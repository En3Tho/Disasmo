using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Disasmo.Utils;

namespace Disasmo.Runner;

public static class Steps
{
    public static async Task<string> RunPublishProject(SymbolInfo symbol, DisasmoSettings settings, string projectPath,
        string targetFramework, string disasmoOutputDir, Action<string> reportLoadingStatus,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!settings.UseCustomRuntime)
            {
                // WIP, see RemoteCheckedJitManager
                return "Only custom locally-built runtimes are supported at the moment :(";
            }

            if (!JitUtils.GetPathToCoreClrChecked(settings, out var clrCheckedFilesDir, out var error))
                return error;

            cancellationToken.ThrowIfCancellationRequested();

            targetFramework = targetFramework.ToLowerInvariant().Trim();

            cancellationToken.ThrowIfCancellationRequested();

            if (targetFramework.StartsWith("net") &&
                float.TryParse(targetFramework.Remove(0, "net".Length), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out float netVer) &&
                netVer >= 5)
            {
                // the project is net5 or newer
            }
            else
            {
                return
                    "Only net5.0 (and later) apps are supported.\nMake sure <TargetFramework>net5.0</TargetFramework> is set in your csproj.";
            }

            if (settings.RunAppMode && settings.UseDotnetPublishForReload)
            {
                // TODO: fix this
                return
                    "\"Run current app\" mode only works with \"dotnet build\" reload strategy, see Options tab.";
            }

            // Validation for Flowgraph tab
            if (settings.FgEnable)
            {
                var phase = settings.FgPhase.Trim();
                if (phase == "*")
                {
                    return "* as a phase name is not supported yet."; // TODO: implement
                }

                if (string.IsNullOrWhiteSpace(settings.GraphvisDotPath) ||
                    !File.Exists(settings.GraphvisDotPath))
                {
                    return
                        "Graphvis is not installed or path to dot.exe is incorrect, see 'Settings' tab.\nGraphvis can be installed from https://graphviz.org/download/";
                }

                if (!settings.JitDumpInsteadOfDisasm)
                {
                    return "Either disable flowgraphs in the 'Flowgraph' tab or enable JitDump.";
                }
            }

            if (settings.CrossgenIsSelected)
            {
                if (settings.UsePGO)
                {
                    return "PGO has no effect on R2R'd code (yet).";
                }

                if (settings.RunAppMode)
                {
                    return "Run mode is not supported for crossgen";
                }

                if (settings.UseTieredJit)
                {
                    return "TieredJIT has no effect on R2R'd code.";
                }

                if (settings.FgEnable)
                {
                    return "Flowgraphs are not tested with crossgen2 yet (in Disasmo)";
                }
            }

            string currentProjectDirPath = Path.GetDirectoryName(projectPath);

            if (string.IsNullOrEmpty(currentProjectDirPath))
            {
                return "Unable to find project directory path";
            }

            ProcessResult publishResult;
            if (settings.UseDotnetPublishForReload)
            {
                reportLoadingStatus($"dotnet publish -r win-x64 -c Release -o ...");

                string dotnetPublishArgs =
                    $"publish -r win-x64 -c Release -o {disasmoOutputDir} --self-contained true /p:PublishTrimmed=false /p:PublishSingleFile=false /p:WarningLevel=0 /p:TreatWarningsAsErrors=false";

                publishResult = await ProcessUtils.RunProcess("dotnet", dotnetPublishArgs, null,
                    currentProjectDirPath, cancellationToken: cancellationToken);
            }
            else
            {
                if (!JitUtils.GetPathToRuntimePack(settings, out _, out error))
                    return error;

                reportLoadingStatus($"dotnet build -c Release -o ...");

                string dotnetBuildArgs =
                    $"build -c Release -o {disasmoOutputDir} /p:WarningLevel=0 /p:TreatWarningsAsErrors=false";

                if (settings.UseNoRestoreFlag)
                    dotnetBuildArgs += " --no-restore --no-dependencies --nologo";

                var fasterBuildArgs = new Dictionary<string, string>
                {
                    ["DOTNET_TC_QuickJitForLoops"] = "1" // slightly speeds up build
                };

                publishResult = await ProcessUtils.RunProcess("dotnet", dotnetBuildArgs, fasterBuildArgs,
                    currentProjectDirPath,
                    cancellationToken: cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (!string.IsNullOrEmpty(publishResult.Error))
            {
                return publishResult.Error;
            }

            // in case if there are compilation errors:
            if (publishResult.Output.Contains(": error"))
            {
                return publishResult.Output;
            }

            if (settings.UseDotnetPublishForReload)
            {
                reportLoadingStatus("Copying files from locally built CoreCLR");

                string dstFolder = disasmoOutputDir;
                if (!Path.IsPathRooted(dstFolder))
                    dstFolder = Path.Combine(currentProjectDirPath, disasmoOutputDir);
                if (!Directory.Exists(dstFolder))
                {
                    return
                        $"Something went wrong, {dstFolder} doesn't exist after 'dotnet publish -r win-x64 -c Release' step";
                }

                var copyClrFilesResult = await ProcessUtils.RunProcess("robocopy",
                    $"/e \"{clrCheckedFilesDir}\" \"{dstFolder}", null, cancellationToken: cancellationToken);

                if (!string.IsNullOrEmpty(copyClrFilesResult.Error))
                {
                    return copyClrFilesResult.Error;
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            return null;
        }
        catch (OperationCanceledException e)
        {
            return e.Message;
        }
        catch (Exception e)
        {
            return e.ToString();
        }
    }

    public static async Task<(bool Error, string Output, string FgPngPath)> RunFinalExe(SymbolInfo currentSymbol,
        DisasmoSettings settings, string currentProjectPath, string disasmoOutDir, Action<string> reportLoadingStatus,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dstFolder = StepsUtil.GetRunInfo(currentSymbol, currentProjectPath, disasmoOutDir, out var fileName,
                out var hostType, out var methodName, out var target);

            if (!settings.RunAppMode && !settings.CrossgenIsSelected)
            {
                await LoaderAppManager.InitLoaderAndCopyTo(dstFolder, settings.CurrentVersion, log =>
                {
                    /*TODO: update UI*/
                }, cancellationToken);
            }

            if (!settings.InitializeEnvVars(methodName, target, out var envVars, out var error))
                return (true, error, null);

            string command =
                $"\"{LoaderAppManager.DisasmoLoaderName}.dll\" \"{fileName}.dll\" \"{hostType}\" \"{methodName}\"";

            if (settings.RunAppMode)
            {
                command = $"\"{fileName}.dll\"";
            }

            string executable = "dotnet";

            if (settings.CrossgenIsSelected)
            {
                if (!settings.FillCrossgenEnvVars(fileName, dstFolder, envVars, out command, out executable,
                        out error))
                    return (true, error, null);

                reportLoadingStatus($"Executing crossgen2...");
            }
            else
            {
                reportLoadingStatus($"Executing DisasmoLoader...");
            }

            if (!settings.UseDotnetPublishForReload && !settings.CrossgenIsSelected)
            {
                if (!JitUtils.GetPathToCoreClrChecked(settings, out var clrCheckedFilesDir, out error))
                    return (true, error, null);

                executable = Path.Combine(clrCheckedFilesDir, "CoreRun.exe");
            }

            ProcessResult result = await ProcessUtils.RunProcess(
                executable, command, envVars, dstFolder, cancellationToken: cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            string finalOutput;
            if (string.IsNullOrEmpty(result.Error))
            {
                finalOutput = StepsUtil.PreprocessOutput(settings, result.Output);
            }
            else
            {
                finalOutput = result.Output + "\nERROR:\n" + result.Error;
            }

            string fgPngPath = null;
            if (settings.FgEnable && settings.JitDumpInsteadOfDisasm)
            {
                var currentFgFile = envVars["DOTNET_JitDumpFgFile"];
                currentFgFile += ".dot";
                if (!File.Exists(currentFgFile))
                {
                    return (true, $"Oops, JitDumpFgFile ('{currentFgFile}') doesn't exist :(\nInvalid Phase name?",
                        null);
                }

                if (new FileInfo(currentFgFile).Length == 0)
                {
                    return (true, $"Oops, JitDumpFgFile ('{currentFgFile}') file is empty :(\nInvalid Phase name?",
                        null);
                }

                var fgLines = File.ReadAllLines(currentFgFile);
                if (fgLines.Count(l => l.StartsWith("digraph FlowGraph")) > 1)
                {
                    int removeTo = fgLines.Select((l, i) => new { line = l, index = i })
                        .Last(i => i.line.StartsWith("digraph FlowGraph")).index;
                    File.WriteAllLines(currentFgFile, fgLines.Skip(removeTo).ToArray());
                }

                cancellationToken.ThrowIfCancellationRequested();

                var pngPath = Path.GetTempFileName();
                string dotExeArgs = $"-Tpng -o\"{pngPath}\" -Kdot \"{currentFgFile}\"";
                ProcessResult dotResult = await ProcessUtils.RunProcess(settings.GraphvisDotPath, dotExeArgs,
                    cancellationToken: cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                if (!File.Exists(pngPath) || new FileInfo(pngPath).Length == 0)
                {
                    return (true, "Graphvis failed:\n" + dotResult.Output + "\n\n" + dotResult.Error, null);
                }

                fgPngPath = pngPath;
            }

            return (false, finalOutput, fgPngPath);
        }
        catch (OperationCanceledException e)
        {
            return (true, e.Message, null);
        }
        catch (Exception e)
        {
            return (true, e.ToString(), null);
        }
    }
}