using System.IO;
using System.Linq;

namespace Disasmo.Runner
{
    public static class JitUtils
    {
        public static bool GetPathToCoreClrChecked(DisasmoSettings settings, out string path, out string error, string arch = "x64")
        {
            var clrCheckedFilesDir = FindJitDirectory(settings.PathToLocalCoreClr, arch);
            if (string.IsNullOrWhiteSpace(clrCheckedFilesDir))
            {
                error = $"Path to a local dotnet/runtime repository is either not set or it's not built for {arch} arch yet" +
                        (settings.CrossgenIsSelected ? "\n(When you use crossgen and target e.g. arm64 you need coreclr built for that arch)" : "") +
                        "\nPlease clone it and build it in `Checked` mode, e.g.:\n\n" +
                        "git clone git@github.com:dotnet/runtime.git\n" +
                        "cd runtime\n" +
                        $"build.cmd Clr+Libs -c Release -rc Checked -a {arch}\n\n";
                path = null;
                return false;
            }

            error = null;
            path = clrCheckedFilesDir;

            return true;
        }

        public static string FindJitDirectory(string basePath, string arch = "x64")
        {
            string jitDir = Path.Combine(basePath, $@"artifacts\bin\coreclr\windows.{arch}.Checked");
            if (Directory.Exists(jitDir))
            {
                return jitDir;
            }

            jitDir = Path.Combine(basePath, $@"artifacts\bin\coreclr\windows.{arch}.Debug");
            if (Directory.Exists(jitDir))
            {
                return jitDir;
            }

            return null;
        }

        public static bool GetPathToRuntimePack(DisasmoSettings settings, out string path, out string error, string arch = "x64")
        {
            if (!GetPathToCoreClrChecked(settings, out path, out error, arch))
            {
                path = null;
                return false;
            }

            string runtimePacksPath = Path.Combine(settings.PathToLocalCoreClr, @"artifacts\bin\runtime");
            string runtimePackPath = null;
            if (Directory.Exists(runtimePacksPath))
            {
                var packs = Directory.GetDirectories(runtimePacksPath, "*-windows-Release-" + arch);
                runtimePackPath = packs.OrderByDescending(i => i).FirstOrDefault();
            }

            if (!Directory.Exists(runtimePackPath))
            {
                error = "Please, build a runtime-pack in your local repo:\n\n" +
                        $"Run 'build.cmd Clr+Libs -c Release -a {arch}' in the repo root\n" +
                        "Don't worry, you won't have to re-build it every time you change something in jit, vm or corelib.";
                return false;
            }

            path = runtimePackPath;
            return true;
        }
    }
}