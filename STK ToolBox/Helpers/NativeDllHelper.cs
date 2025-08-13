using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace STK_ToolBox.Helpers
{
    internal static class NativeDllHelper
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        public static void EnsureLoaded(string dllName, params string[] pathHints)
        {
            var tried = new StringBuilder();
            string[] candidates =
            {
                AppDomain.CurrentDomain.BaseDirectory,
                @"D:\STK ToolBox",
                @"D:\STK ToolBox\Bin",
                @"C:\MELSEC\CCLink\Bin",
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"MELSOFT\CCLink\Bin")
            };
            if (pathHints != null && pathHints.Length > 0)
            {
                var tmp = new string[candidates.Length + pathHints.Length];
                candidates.CopyTo(tmp, 0);
                pathHints.CopyTo(tmp, candidates.Length);
                candidates = tmp;
            }

            foreach (var dir in candidates)
            {
                if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) continue;
                try
                {
                    SetDllDirectory(dir);
                    var full = Path.Combine(dir, dllName);
                    tried.AppendLine(full);
                    if (!File.Exists(full)) continue;
                    var h = LoadLibrary(full);
                    if (h != IntPtr.Zero) { FreeLibrary(h); return; }
                }
                catch { /* continue */ }
            }

            int winErr = Marshal.GetLastWin32Error();
            string arch = Environment.Is64BitProcess ? "x64" : "x86";
            throw new DllNotFoundException(
                $"네이티브 DLL '{dllName}'을(를) 로드하지 못했습니다. (LastError={winErr}, 프로세스={arch})\n" +
                "원인: 경로 없음 / x86·x64 불일치 / 의존 DLL 누락.\n\n[시도한 경로]\n" + tried.ToString());
        }
    }
}
