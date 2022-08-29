using System;
using System.Collections.Generic;
using System.IO;

namespace Disasmo.Runner;

public record DisasmoSettings
{
    public string PathToLocalCoreClr { get; set; } = "";
    public bool JitDumpInsteadOfDisasm { get; set; }
    public string? CustomEnvVars { get; set; }
    public string? Crossgen2Args { get; set; }
    public bool ShowAsmComments { get; set; }
    public Version CurrentVersion { get; set; } = new();
    public bool AllowDisasmInvocations { get; set; }
    public bool UseDotnetPublishForReload { get; set; }
    public bool UseDotnetBuildForReload { get; set; }
    public bool RunAppMode { get; set; }
    public bool PrintInlinees { get; set; }
    public bool PresenterMode { get; set; }
    public bool UseNoRestoreFlag { get; set; }
    public bool UseTieredJit { get; set; }
    public bool UsePGO { get; set; }
    public bool UseCustomRuntime { get; set; }
    public string? SelectedCustomJit { get; set; }
    public string? GraphvisDotPath { get; set; }
    public bool FgEnable { get; set; }
    public string? FgPhase { get; set; }
    public bool CrossgenIsSelected { get; set; }

    public const string DefaultJit = "clrjit.dll";
}

public static class DisasmoSettingsExtensions
{
    public static void FillWithUserVars(this DisasmoSettings settings, Dictionary<string, string> dictionary)
    {
        if (string.IsNullOrWhiteSpace(settings.CustomEnvVars))
            return;

        var pairs = settings.CustomEnvVars!.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var pair in pairs)
        {
            var parts = pair.Split('=');
            if (parts.Length == 2)
                dictionary[parts[0].Trim()] = parts[1].Trim();
        }
    }

    public static bool InitializeEnvVars(this DisasmoSettings settings, string methodName, string target,
        out Dictionary<string, string> result, out string error)
    {
        var envVars = new Dictionary<string, string>();
        if (settings.JitDumpInsteadOfDisasm)
            envVars["DOTNET_JitDump"] = target;
        else if (settings.PrintInlinees)
            envVars["DOTNET_JitPrintInlinedMethods"] = target;
        else
            envVars["DOTNET_JitDisasm"] = target;

        if (!string.IsNullOrWhiteSpace(settings.SelectedCustomJit) && !settings.CrossgenIsSelected &&
            !settings.SelectedCustomJit!.Equals(DisasmoSettings.DefaultJit, StringComparison.InvariantCultureIgnoreCase))
        {
            envVars["DOTNET_AltJitName"] = settings.SelectedCustomJit;
            envVars["DOTNET_AltJit"] = target;
        }

        envVars["DOTNET_TieredPGO"] = settings.UsePGO ? "1" : "0";

        if (!settings.UseDotnetPublishForReload)
        {
            if (!JitUtils.GetPathToRuntimePack(settings, out var runtimePackPath, out error))
            {
                result = null;
                return false;
            }

            // tell jit to look for BCL libs in the locally built runtime pack
            envVars["CORE_LIBRARIES"] = runtimePackPath;
        }

        envVars["DOTNET_TieredCompilation"] = settings.UseTieredJit ? "1" : "0";

        // User is free to override any of those ^
        settings.FillWithUserVars(envVars);

        if (settings.FgEnable)
        {
            if (methodName == "*")
            {
                error = "Flowgraph for classes (all methods) is not supported yet.";
                result = null;
                return false;
            }

            envVars["DOTNET_JitDumpFg"] = target;
            envVars["DOTNET_JitDumpFgDot"] = "1";
            envVars["DOTNET_JitDumpFgPhase"] = settings.FgPhase!.Trim();
            envVars["DOTNET_JitDumpFgFile"] = Path.GetTempFileName();
        }

        error = null;
        result = envVars;
        return true;
    }

    public static bool FillCrossgenEnvVars(this DisasmoSettings settings, string fileName, string dstFolder,
        Dictionary<string, string> envVars,
         out string command, out string executable, out string error)
    {
        if (!JitUtils.GetPathToCoreClrChecked(settings, out var clrCheckedFilesDir, out error) ||
            !JitUtils.GetPathToRuntimePack(settings, out var runtimePackPath, out error))
        {
            command = "";
            executable = "";
            return false;
        }

        command = "";
        executable = Path.Combine(clrCheckedFilesDir, "crossgen2", "crossgen2.exe");

        command += " --out aot ";

        foreach (var envVar in envVars)
        {
            var keyLower = envVar.Key.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(keyLower)
                || (keyLower.StartsWith("dotnet_") == false && keyLower.StartsWith("complus_") == false))
            {
                continue;
            }

            keyLower = keyLower
                .Replace("dotnet_jitdump", "--codegenopt:ngendump")
                .Replace("dotnet_jitdisasm", "--codegenopt:ngendisasm")
                .Replace("dotnet_", "--codegenopt:")
                .Replace("complus_", "--codegenopt:");
            command += keyLower + "=\"" + envVar.Value + "\" ";
        }

        // These are needed for faster crossgen itself - they're not changing output codegen
        envVars["DOTNET_TieredPGO"] = "0";
        envVars["DOTNET_ReadyToRun"] = "1";
        envVars["DOTNET_TC_QuickJitForLoops"] = "1";
        envVars["DOTNET_TieredCompilation"] = "1";
        command += settings.Crossgen2Args + $" \"{fileName}.dll\" ";

        if (settings.UseDotnetPublishForReload)
        {
            // Reference everything in the publish dir
            command += $" -r: \"{dstFolder}\\*.dll\" ";
        }
        else
        {
            // the runtime pack we use doesn't contain corelib so let's use "checked" corelib
            // TODO: build proper core_root with release version of corelib
            var corelib = Path.Combine(clrCheckedFilesDir, "System.Private.CoreLib.dll");
            command += $" -r: \"{runtimePackPath}\\*.dll\" -r: \"{corelib}\" ";
        }

        return true;
    }
}