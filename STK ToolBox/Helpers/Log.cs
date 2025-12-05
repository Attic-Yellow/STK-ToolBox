using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace STK_ToolBox.Helpers
{
    /// <summary>
    /// STK ToolBox 전용 로그 헬퍼.
    /// 
    /// 특징:
    /// - 날짜별 로그 파일 생성 (파일명: stk_yyyyMMdd.log)
    /// - INFO / WARN / ERROR 레벨 지원
    /// - 예외 발생 시 앱이 죽지 않도록 모든 로깅은 try-catch 내부에서 처리
    /// - 멀티스레드 환경을 고려하여 파일 접근 시 lock(_lock)으로 동기화
    /// - 파일 경로는 D:\STK ToolBox\Logs 에 고정(필요 시 수정 가능)
    /// </summary>
    public static class Log
    {
        #region ───────── Fields / Paths ─────────

        private static readonly object _lock = new object();

        /// <summary>
        /// 로그 루트 경로.
        /// </summary>
        private static readonly string _root = @"D:\STK ToolBox\Logs";

        /// <summary>
        /// 오늘 날짜에 해당하는 로그 파일 경로.
        /// 예: D:\STK ToolBox\Logs\stk_20251204.log
        /// </summary>
        private static string TodayPath =>
            Path.Combine(_root, $"stk_{DateTime.Now:yyyyMMdd}.log");

        #endregion

        #region ───────── Directory Ensure ─────────

        /// <summary>
        /// 로그 폴더가 없는 경우 자동 생성.
        /// 실패해도 로깅은 앱 죽지 않도록 무시.
        /// </summary>
        private static void EnsureDir()
        {
            try
            {
                if (!Directory.Exists(_root))
                    Directory.CreateDirectory(_root);
            }
            catch
            {
                /* ignore: 로깅 시스템은 절대 앱을 죽여선 안 됨 */
            }
        }

        #endregion

        #region ───────── Public Logging APIs ─────────

        public static void Info(string msg) => Write("INFO", msg);

        public static void Warn(string msg) => Write("WARN", msg);

        public static void Error(string msg) => Write("ERRO", msg);

        /// <summary>
        /// 예외 객체를 함께 기록하는 Error 로깅.
        /// </summary>
        public static void Error(Exception ex, string message = null)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(message))
                sb.AppendLine(message);

            sb.AppendLine(ex.ToString());

            Write("ERRO", sb.ToString());
        }

        #endregion

        #region ───────── Core Write Logic ─────────

        /// <summary>
        /// 로그 파일에 1라인 기록.
        /// 포맷:
        /// yyyy-MM-dd HH:mm:ss.fff [LEVEL] (PID xxx) message
        /// </summary>
        private static void Write(string level, string msg)
        {
            try
            {
                EnsureDir();

                string line =
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] " +
                    $"(PID {Process.GetCurrentProcess().Id}) {msg}";

                lock (_lock)
                {
                    File.AppendAllText(TodayPath, line + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch
            {
                // Logging must never cause the application to crash.
            }
        }

        #endregion
    }
}
