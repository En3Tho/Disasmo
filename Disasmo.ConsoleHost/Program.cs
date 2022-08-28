// See https://aka.ms/new-console-template for more information

using Disasmo.Runner;

var settings = new DisasmoSettings()
{
    PathToLocalCoreClr = @"G:\source\repos\dotnet\runtime",
    JitDumpInsteadOfDisasm = false,
    CustomEnvVars = null,
    Crossgen2Args = null,
    ShowAsmComments = true,
    CurrentVersion = new(),
    AllowDisasmInvocations = true,
    UseDotnetPublishForReload = false,
    UseDotnetBuildForReload = true,
    RunAppMode = false,
    PrintInlinees = false,
    PresenterMode = false,
    UseNoRestoreFlag = false,
    UseTieredJit = false,
    UsePGO = false,
    UseCustomRuntime = true,
    SelectedCustomJit = null,
    GraphvisDotPath = null,
    FgEnable = false,
    FgPhase = null,
    CrossgenIsSelected = false,
};

var symbolInfo = new SymbolInfo()
{
    MethodName = "NonOptimized",
    ClassName = "Program",
    QualifiedClassName = "Program",
    IsLocalFunction = false
};

var projectPath = @"G:\source\repos\En3Tho\dotnet\JitApps\Multiplication\Multiplication.csproj";
var tfm = "net7.0";
var disasmoOutputDir = @"G:\source\repos\En3Tho\dotnet\JitApps\Multiplication\Disasmo";
var reporter = (string status) => Console.WriteLine(status);

var err = await Steps.RunPublishProject(symbolInfo, settings, projectPath, tfm, disasmoOutputDir, reporter);
if (err is { })
{
    Console.WriteLine(err);
}
else
{
    var output = await Steps.RunFinalExe(symbolInfo, settings, projectPath, disasmoOutputDir, reporter);
    Console.WriteLine($"IsError: {output.Error}");
    Console.WriteLine($"Output: {output.Output}");
    Console.WriteLine($"PngFilePath: {output.FgPngPath}");
}