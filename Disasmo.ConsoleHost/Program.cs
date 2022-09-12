// See https://aka.ms/new-console-template for more information

using System.Diagnostics;
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
    TargetFramework = "net7.0"
};

var symbolInfo = new SymbolInfo()
{
    MethodName = null,//"Equal1",
    ClassName = "Program",
    QualifiedClassName = "Program",
    IsLocalFunction = false
};

var projectPath = @"G:\source\repos\En3Tho\dotnet\JitApps\Multiplication\Multiplication.csproj";
var disasmoOutputDir = @"G:\source\repos\En3Tho\dotnet\JitApps\Multiplication\Disasmo";
var reporter = (string status) => Console.WriteLine(status);

var err = await Steps.RunPublishProject(symbolInfo, settings, projectPath, disasmoOutputDir, reporter);
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

    var filePath = $"{disasmoOutputDir}\\output.asm";
    File.WriteAllText(filePath, output.Output);
    var codePath = @"C:\Users\RZRL\AppData\Local\Programs\Microsoft VS Code\Code.exe";
    Process.Start($"{codePath} \"{filePath}\"");
}