using System.IO;
using Disasmo.Utils;

namespace Disasmo.Runner;

public static class StepsUtil
{
    public static string GetRunInfo(SymbolInfo currentSymbol, string currentProjectPath, string disasmoOutDir,
        out string fileName, out string hostType, out string methodName, out string target)
    {
        string dstFolder = disasmoOutDir;
        if (!Path.IsPathRooted(dstFolder))
            dstFolder = Path.Combine(Path.GetDirectoryName(currentProjectPath)!, disasmoOutDir);

        // TODO: respect AssemblyName property (if it doesn't match csproj name)
        fileName = Path.GetFileNameWithoutExtension(currentProjectPath);

        if (currentSymbol.MethodName != null)
        {
            if (currentSymbol.IsLocalFunction)
            {
                // just print them all, I don't know how to get "g__%MethodName|0_0" ugly name out of
                // IMethodSymbol in order to pass it to JitDisasm. Ugh, I hate it.
                target = "*" + currentSymbol.ClassName + ":*";
                hostType = currentSymbol.QualifiedClassName;
                methodName = "*";
            }
            else
            {
                target = "*" + currentSymbol.ClassName + ":" + currentSymbol.MethodName;
                hostType = currentSymbol.QualifiedClassName;
                methodName = currentSymbol.MethodName;

                if (hostType.EndsWith(">$"))
                {
                    // A hack for local/global functions
                    target = $"<{currentSymbol.ClassName}>g__{methodName}*";
                }
            }
        }
        else
        {
            // the whole class
            target = currentSymbol.ClassName + ":*";
            hostType = currentSymbol.QualifiedClassName;
            methodName = "*";
        }

        return dstFolder;
    }

    public static string PreprocessOutput(DisasmoSettings settings, string output)
    {
        if (settings.JitDumpInsteadOfDisasm || settings.PrintInlinees)
            return output;
        return ComPlusDisassemblyPrettifier.Prettify(output, !settings.ShowAsmComments && !settings.RunAppMode);
    }
}