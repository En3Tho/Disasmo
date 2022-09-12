﻿using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Input;
using Disasmo.Properties;
using Disasmo.Runner;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;

namespace Disasmo
{
    public class SettingsViewModel : ViewModelBase
    {
        private string _pathToLocalCoreClr;
        private bool _jitDumpInsteadOfDisasm;
        private string _customEnvVars;
        private string _crossgen2Args;
        private bool _showAsmComments;
        private bool _updateIsAvailable;
        private Version _currentVersion;
        private Version _availableVersion;
        private bool _allowDisasmInvocations;
        private bool _useDotnetPublishForReload;
        private bool _useDotnetBuildForReload;
        private bool _runAppMode;
        private bool _printInlinees;
        private bool _presenterMode;
        private bool _useNoRestoreFlag;
        private bool _useTieredJit;
        private bool _usePGO;
        private bool _useCustomRuntime;
        private ObservableCollection<string> _customJits;
        private string _selectedCustomJit;
        private string _graphvisDot;
        private bool _fgEnable;
        private string _fgPhase;

        public SettingsViewModel()
        {
            PathToLocalCoreClr = Settings.Default.PathToCoreCLR_V8;
            ShowAsmComments = Settings.Default.ShowAsmComments_V8;
            CustomEnvVars = Settings.Default.CustomEnvVars3_V11.Replace(";;", Environment.NewLine);
            Crossgen2Args = Settings.Default.CrossgenArgs_V3;
            JitDumpInsteadOfDisasm = Settings.Default.JitDumpInsteadOfDisasm_V8;
            AllowDisasmInvocations = Settings.Default.AllowDisasmInvocations_V8;
            UseDotnetBuildForReload = Settings.Default.UseDotnetBuildForReload_V8;
            RunAppMode = Settings.Default.RunAppMode_V8;
            UseNoRestoreFlag = Settings.Default.UseNoRestoreFlag_V8;
            PresenterMode = Settings.Default.PresenterMode;
            UpdateIsAvailable = false;
            UseTieredJit = Settings.Default.UseTieredJit_V3;
            UseCustomRuntime = Settings.Default.UseCustomRuntime_V3;
            GraphvisDotPath = Settings.Default.GraphvisDotPath;
            FgPhase = Settings.Default.FgPhase;
            FgEnable = Settings.Default.FgEnable;
            PrintInlinees = Settings.Default.PrintInlinees_V2;
            UsePGO = Settings.Default.UsePGO;
            CheckUpdates();
        }

        private async void CheckUpdates()
        {
            CurrentVersion = DisasmoPackage.Current?.GetCurrentVersion();
            AvailableVersion = await DisasmoPackage.GetLatestVersionOnline();
            if (CurrentVersion != null && AvailableVersion > CurrentVersion)
                UpdateIsAvailable = true;
        }

        public bool FgEnable
        {
            get => _fgEnable;
            set
            {
                Set(ref _fgEnable, value);
                Settings.Default.FgEnable = value;
                Settings.Default.Save();
                if (value)
                {
                    JitDumpInsteadOfDisasm = true;
                }
            }
        }

        public string FgPhase
        {
            get => _fgPhase;
            set
            {
                Set(ref _fgPhase, value);
                Settings.Default.FgPhase = value;
                Settings.Default.Save();
            }
        }

        public string GraphvisDotPath
        {
            get => _graphvisDot;
            set
            {
                Set(ref _graphvisDot, value);
                Settings.Default.GraphvisDotPath = value;
                Settings.Default.Save();
            }
        }

        public string PathToLocalCoreClr
        {
            get => _pathToLocalCoreClr;
            set
            {
                Set(ref _pathToLocalCoreClr, value);
                Settings.Default.PathToCoreCLR_V8 = value;
                Settings.Default.Save();

                if (PopulateCustomJits())
                {
                    return;
                }

                SelectedCustomJit = null;
                CustomJits?.Clear();
            }
        }

        public bool IsNonCustomDotnetAotMode()
        {
            return !UseCustomRuntime &&
                   (SelectedCustomJit == Crossgen || SelectedCustomJit == Ilc);
        }

        public const string DefaultJit = "clrjit.dll";
        public const string Crossgen = "crossgen2.dll (R2R)";
        public const string Ilc = "ilc (NativeAOT)";

        public bool PopulateCustomJits()
        {
            if (!UseCustomRuntime)
            {
                CustomJits = new ObservableCollection<string>();
                CustomJits.Add(DefaultJit);

                // TODO:
                //CustomJits.Add(Crossgen);
                //CustomJits.Add(Ilc);
                SelectedCustomJit = CustomJits[0];
                return true;
            }

            if (!string.IsNullOrWhiteSpace(_pathToLocalCoreClr))
            {
                string jitDir = JitUtils.FindJitDirectory(_pathToLocalCoreClr);
                if (jitDir != null)
                {
                    string[] jits = Directory.GetFiles(jitDir, "clrjit*.dll");
                    CustomJits = new ObservableCollection<string>(jits.Select(Path.GetFileName));
                    SelectedCustomJit = CustomJits.FirstOrDefault(j => j == DefaultJit);
                    if (SelectedCustomJit != null)
                    {
                        CustomJits.Add(Crossgen);
                    }
                    return true;
                }
            }
            return false;
        }

        public ObservableCollection<string> CustomJits
        {
            get => _customJits;
            set => Set(ref _customJits, value);
        }

        public string SelectedCustomJit
        {
            get => _selectedCustomJit;
            set
            {
                if (value?.StartsWith("crossgen") == true)
                {
                    RunAppMode = false;
                    UseTieredJit = false;
                    UsePGO = false;
                    JitDumpInsteadOfDisasm = false;
                }
                Set(ref _selectedCustomJit, value);
            }
        }

        public bool CrossgenIsSelected => SelectedCustomJit?.StartsWith("crossgen") == true;

        public bool RunAppMode
        {
            get => _runAppMode;
            set
            {
                Set(ref _runAppMode, value);
                Settings.Default.RunAppMode_V8 = value;
                Settings.Default.Save();
            }
        }

        public bool PrintInlinees
        {
            get => _printInlinees;
            set
            {
                Set(ref _printInlinees, value);
                if (value)
                {
                    // Reset "Use JitDump" flag which should also reset FlowGraph flag
                    JitDumpInsteadOfDisasm = false;
                }

                Settings.Default.PrintInlinees_V2 = value;
                Settings.Default.Save();
            }
        }

        public bool UsePGO
        {
            get => _usePGO;
            set
            {
                Set(ref _usePGO, value);
                if (value) 
                    this.UseTieredJit = true;

                Settings.Default.UsePGO = value;
                Settings.Default.Save();
            }
        }

        public bool UseNoRestoreFlag
        {
            get => _useNoRestoreFlag;
            set
            {
                Set(ref _useNoRestoreFlag, value);
                Settings.Default.UseNoRestoreFlag_V8 = value;
                Settings.Default.Save();
            }
        }

        public bool PresenterMode
        {
            get => _presenterMode;
            set
            {
                Set(ref _presenterMode, value);
                Settings.Default.PresenterMode = value;
                Settings.Default.Save();
            }
        }

        public bool UseCustomRuntime
        {
            get => _useCustomRuntime;
            set
            {
                Set(ref _useCustomRuntime, value);
                Settings.Default.UseCustomRuntime_V3 = value;
                Settings.Default.Save();
                if (!value)
                {
                    JitDumpInsteadOfDisasm = false;
                    UseDotnetPublishForReload = false;
                    UseDotnetBuildForReload = true;
                    PrintInlinees = false;
                }
                PopulateCustomJits();
            }
        }

        public bool UseDotnetPublishForReload
        {
            get => _useDotnetPublishForReload;
            set
            {
                Set(ref _useDotnetPublishForReload, value);
                Set(ref _useDotnetBuildForReload, !value);
                Settings.Default.UseDotnetBuildForReload_V8 = !value;
                Settings.Default.Save();
            }
        }

        public bool UseDotnetBuildForReload
        {
            get => _useDotnetBuildForReload;
            set
            {
                Set(ref _useDotnetBuildForReload, value);
                Set(ref _useDotnetPublishForReload, !value);
                Settings.Default.UseDotnetBuildForReload_V8 = value;
                Settings.Default.Save();
            }
        }

        public bool JitDumpInsteadOfDisasm
        {
            get => _jitDumpInsteadOfDisasm;
            set
            {
                Set(ref _jitDumpInsteadOfDisasm, value);
                Settings.Default.JitDumpInsteadOfDisasm_V8 = value;
                Settings.Default.Save();
                if (!value)
                {
                    FgEnable = false;
                }
                else
                {
                    PrintInlinees = false;
                }
            }
        }

        public bool UseTieredJit
        {
            get => _useTieredJit;
            set
            {
                Set(ref _useTieredJit, value);
                Settings.Default.UseTieredJit_V3 = value;
                Settings.Default.Save();
            }
        }

        public bool ShowAsmComments
        {
            get => _showAsmComments;
            set
            {
                Set(ref _showAsmComments, value);
                Settings.Default.ShowAsmComments_V8 = value;
                Settings.Default.Save();
            }
        }

        public string CustomEnvVars
        {
            get => _customEnvVars;
            set
            {
                Set(ref _customEnvVars, value);
                Settings.Default.CustomEnvVars3_V11 = value;
                Settings.Default.Save();
            }
        }

        public string Crossgen2Args
        {
            get => _crossgen2Args;
            set
            {
                Set(ref _crossgen2Args, value);
                Settings.Default.CrossgenArgs_V3 = value;
                Settings.Default.Save();
            }
        }

        public bool UpdateIsAvailable
        {
            get => _updateIsAvailable;
            set { Set(ref _updateIsAvailable, value); }
        }

        public Version CurrentVersion
        {
            get => _currentVersion;
            set => Set(ref _currentVersion, value);
        }

        public Version AvailableVersion
        {
            get => _availableVersion;
            set => Set(ref _availableVersion, value);
        }

        public ICommand BrowseCommand => new RelayCommand(() =>
        {
            var dialog = new FolderBrowserDialog();
            var result = dialog.ShowDialog();
            if (result == DialogResult.OK)
                PathToLocalCoreClr = dialog.SelectedPath;
        });

        public bool AllowDisasmInvocations
        {
            get => _allowDisasmInvocations;
            set
            {
                Set(ref _allowDisasmInvocations, value);
                Settings.Default.AllowDisasmInvocations_V8 = value;
                Settings.Default.Save();
            }
        }
    }
}
