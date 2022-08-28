using Disasmo.Runner;

namespace Disasmo;

public static class DisasmoSettingsExtensions
{
    public static DisasmoSettings ToDisasmoSettings(this SettingsViewModel settingsViewModel)
    {
        return new()
        {
            PathToLocalCoreClr = settingsViewModel.PathToLocalCoreClr,
            JitDumpInsteadOfDisasm = settingsViewModel.JitDumpInsteadOfDisasm,
            CustomEnvVars = settingsViewModel.CustomEnvVars,
            Crossgen2Args = settingsViewModel.Crossgen2Args,
            ShowAsmComments = settingsViewModel.ShowAsmComments,
            CurrentVersion = settingsViewModel.CurrentVersion,
            AllowDisasmInvocations = settingsViewModel.AllowDisasmInvocations,
            UseDotnetPublishForReload = settingsViewModel.UseDotnetPublishForReload,
            UseDotnetBuildForReload = settingsViewModel.UseDotnetBuildForReload,
            RunAppMode = settingsViewModel.RunAppMode,
            PrintInlinees = settingsViewModel.PrintInlinees,
            PresenterMode = settingsViewModel.PresenterMode,
            UseNoRestoreFlag = settingsViewModel.UseNoRestoreFlag,
            UseTieredJit = settingsViewModel.UseTieredJit,
            UsePGO = settingsViewModel.UsePGO,
            UseCustomRuntime = settingsViewModel.UseCustomRuntime,
            SelectedCustomJit = settingsViewModel.SelectedCustomJit,
            GraphvisDotPath = settingsViewModel.GraphvisDotPath,
            FgEnable = settingsViewModel.FgEnable,
            FgPhase = settingsViewModel.FgPhase,
            CrossgenIsSelected = settingsViewModel.CrossgenIsSelected
        };
    }
}