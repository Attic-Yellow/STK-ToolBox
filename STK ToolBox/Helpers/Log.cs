using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace STK_ToolBox.Helpers
{
    public static class Log
    {
        private static readonly object _lock = new object();
        private static readonly string _root =
            @"D:\STK ToolBox\Logs"; // 원하는 경로. 없으면 자동 생성.

        private static string TodayPath =>
            Path.Combine(_root, $"stk_{DateTime.Now:yyyyMMdd}.log");

        private static void EnsureDir()
        {
            try { if (!Directory.Exists(_root)) Directory.CreateDirectory(_root); }
            catch { /* ignore */ }
        }

        public static void Info(string msg) => Write("INFO", msg);
        public static void Warn(string msg) => Write("WARN", msg);
        public static void Error(string msg) => Write("ERRO", msg);

        public static void Error(Exception ex, string message = null)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(message)) sb.AppendLine(message);
            sb.AppendLine(ex.ToString());
            Write("ERRO", sb.ToString());
        }

        private static void Write(string level, string msg)
        {
            try
            {
                EnsureDir();
                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] " +
                           $"(PID {Process.GetCurrentProcess().Id}) {msg}";
                lock (_lock)
                {
                    File.AppendAllText(TodayPath, line + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch { /* logging must not throw */ }
        }
    }
}
