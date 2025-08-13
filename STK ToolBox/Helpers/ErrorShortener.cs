using System;

namespace STK_ToolBox.Helpers
{
    public static class ErrorShortener
    {
        public static string Short(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "UNKNOWN ERROR";

            var s = raw.ToLowerInvariant();

            if (s.Contains("dllnotfound") || s.Contains("cclnkif.dll"))
                return "DLL LOAD FAIL";

            if (s.Contains("badimageformat") || s.Contains("x86") && s.Contains("x64"))
                return "ARCH MISMATCH (x86 ONLY)";

            if (s.Contains("open fail") || s.Contains("보드 열기 실패") || s.Contains("rc="))
                return "BOARD OPEN FAIL";

            if (s.Contains("read") && s.Contains("fail"))
                return "READ FAIL";

            if (s.Contains("write") && s.Contains("fail"))
                return "WRITE FAIL";

            return "ERROR";
        }
    }
}
