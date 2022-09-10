using EnvDTE;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using Microsoft.CodeAnalysis;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Disasmo.Runner;
using Disasmo.Utils;
using Project = EnvDTE.Project;
using Task = System.Threading.Tasks.Task;
using Disasmo.ViewModels;
using Microsoft.IO;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Properties;

namespace Disasmo
{
    public class MainViewModel : ViewModelBase
    {
        private string _output;
        private string _previousOutput;
        private string _loadingStatus;
        private string _stopwatchStatus;
        private string[] _jitDumpPhases;
        private bool _isLoading;
        private ISymbol _currentSymbol;
        private bool _success;
        private string _currentProjectPath;
        private string _fgPngPath;
        private string DisasmoOutDir = "";

        // let's use new name for the temp folder each version to avoid possible issues (e.g. changes in the Disasmo.Loader)
        private string DisasmoFolder => "Disasmo-v" + DisasmoPackage.Current?.GetCurrentVersion();

        public SettingsViewModel SettingsVm { get; } = new SettingsViewModel();
        public IntrinsicsViewModel IntrinsicsVm { get; } = new IntrinsicsViewModel();

        public event Action MainPageRequested;

        public MainViewModel()
        {
            if (IsInDesignMode)
            {
                // Some design-time data for development
                JitDumpPhases = new[]
                {
                    "Pre-import",
                    "Profile incorporation",
                    "Importation",
                    "Morph - Add internal blocks",
                    "Compute edge weights (1, false)",
                    "Build SSA representation",
                };
            }
        }

        public string[] JitDumpPhases
        {
            get => _jitDumpPhases;
            set => Set(ref _jitDumpPhases, value);
        }

        public string Output
        {
            get => _output;
            set
            {
                if (!string.IsNullOrWhiteSpace(_output))
                    PreviousOutput = _output;
                Set(ref _output, value);

                const string phasePrefix = "*************** Starting PHASE ";
                JitDumpPhases = (Output ?? "")
                    .Split('\n')
                    .Where(l => l.StartsWith(phasePrefix))
                    .Select(i => i.Replace(phasePrefix, ""))
                    .ToArray();
            }
        }

        public string PreviousOutput
        {
            get => _previousOutput;
            set => Set(ref _previousOutput, value);
        }

        public string LoadingStatus
        {
            get => _loadingStatus;
            set => Set(ref _loadingStatus, value);
        }

        public CancellationTokenSource UserCts { get; set; }

        public CancellationToken UserCt => UserCts?.Token ?? default;

        public void ThrowIfCanceled()
        {
            if (UserCts?.IsCancellationRequested == true)
                throw new OperationCanceledException();
        }

        public ICommand CancelCommand => new RelayCommand(() =>
        {
            try
            {
                UserCts?.Cancel();
            }
            catch
            {
            }
        });

        public bool Success
        {
            get => _success;
            set => Set(ref _success, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (!_isLoading && value)
                {
                    UserCts = new CancellationTokenSource();
                }

                Set(ref _isLoading, value);
            }
        }

        public string StopwatchStatus
        {
            get => _stopwatchStatus;
            set => Set(ref _stopwatchStatus, value);
        }

        public string FgPngPath
        {
            get => _fgPngPath;
            set => Set(ref _fgPngPath, value);
        }


        public ICommand RefreshCommand => new RelayCommand(() => RunOperationVSAsync(_currentSymbol, SettingsVm));

        public ICommand RunDiffWithPrevious => new RelayCommand(() => IdeUtils.RunDiffTools(PreviousOutput, Output));

        public async Task<string> RunFinalExeVSAsync(SettingsViewModel settings, ISymbol currentSymbol,
            string currentProjectPath, string disasmoOutDir)
        {
            try
            {
                await DisasmoPackage.Current.JoinableTaskFactory.SwitchToMainThreadAsync();

                Success = false;
                IsLoading = true;
                FgPngPath = null;
                LoadingStatus = "Loading...";

                var (isError, output, fgPngPath) = await Steps.RunFinalExe(currentSymbol.ToSymbolInfo(), settings.ToDisasmoSettings(), currentProjectPath,
                    disasmoOutDir, status => LoadingStatus = status, UserCt);

                Success = !isError;

                if (fgPngPath != null)
                {
                    FgPngPath = fgPngPath;
                }

                return output;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private UnconfiguredProject GetUnconfiguredProject(EnvDTE.Project project)
        {
            var context = project as IVsBrowseObjectContext;
            if (context == null && project != null)
                context = project.Object as IVsBrowseObjectContext;

            return context?.UnconfiguredProject;
        }

        public async Task RunOperationVSAsync(ISymbol symbol, SettingsViewModel settings)
        {
            if (symbol == null)
                return;

            if (symbol.IsGenericMethod())
            {
                Output = "Generic methods are not supported yet.";
                return;
            }

            await DisasmoPackage.Current.JoinableTaskFactory.SwitchToMainThreadAsync();

            var stopwatch = Stopwatch.StartNew();
            DTE dte = IdeUtils.DTE();

            try
            {
                IsLoading = true;
                FgPngPath = null;
                await Task.Delay(50);
                MainPageRequested?.Invoke();
                Success = false;
                _currentSymbol = symbol;
                Output = "";

                // Find Release-x64 configuration:
                Project currentProject = dte.GetActiveProject();
                UnconfiguredProject unconfiguredProject = GetUnconfiguredProject(currentProject);

                // it will throw "Release config was not found" to the Output if there is no such config in the project
                ProjectConfiguration releaseConfig = await unconfiguredProject.Services.ProjectConfigurationsService
                    .GetProjectConfigurationAsync("Release");
                ConfiguredProject configuredProject =
                    await unconfiguredProject.LoadConfiguredProjectAsync(releaseConfig);
                IProjectProperties projectProperties =
                    configuredProject.Services.ProjectPropertiesProvider.GetCommonProperties();

                _currentProjectPath = currentProject.FileName;

                if (string.IsNullOrWhiteSpace(_currentProjectPath))
                    return;

                dte.SaveAllActiveDocuments();

                string targetFramework = await projectProperties.GetEvaluatedPropertyValueAsync("TargetFramework");
                targetFramework = targetFramework.ToLowerInvariant().Trim();
                DisasmoOutDir = Path.Combine(await projectProperties.GetEvaluatedPropertyValueAsync("OutputPath"), DisasmoFolder);

                var error = await Steps.RunPublishProject(symbol.ToSymbolInfo(), settings.ToDisasmoSettings(), _currentProjectPath, targetFramework, DisasmoOutDir,
                    status => LoadingStatus = status, UserCt);
                if (error != null)
                {
                    Output = error;
                }
                else
                {
                    var output = await RunFinalExeVSAsync(settings, _currentSymbol, _currentProjectPath, DisasmoOutDir);
                    if (output != null)
                        Output = output;
                }
            }
            finally
            {
                IsLoading = false;
                stopwatch.Stop();
                StopwatchStatus = $"Disasm took {stopwatch.Elapsed.TotalSeconds:F1} s.";
            }
        }
    }
}