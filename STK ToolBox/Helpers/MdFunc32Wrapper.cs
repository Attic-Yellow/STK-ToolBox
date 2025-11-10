using System;
using System.Runtime.InteropServices;

namespace STK_ToolBox.Helpers
{
    /// <summary>
    /// Mitsubishi Board Utility (MdFun32.dll) 경량 래퍼
    /// - ViewModel은 Open/Close와 TryReadBit/TryWriteBit만 호출하면 됨
    /// - 주소는 "X000A", "Y0010", "M100", "D200" 등 문자열 그대로 전달
    /// </summary>
    public static class MdFunc32Wrapper
    {
        // 실행파일 폴더 또는 System32(또는 SysWOW64)에 MdFun32.dll이 있어야 함.
        private const string DLL = "MdFun32.dll";

        // ── P/Invoke ───────────────────────────────────────────────────────────
        [DllImport(DLL, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private static extern short mdOpen(short path_no);

        [DllImport(DLL, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private static extern short mdClose(short path_no);

        [DllImport(DLL, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private static extern short mdDevR(short path_no, string device, ref int data);

        [DllImport(DLL, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private static extern short mdDevSet(short path_no, string device, int data);

        // 필요 시 블록 I/O용 (미사용이지만 남겨둠)
        [DllImport(DLL, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private static extern short mdReceive(short path_no, short station_no, short start_io, short size, byte[] buffer);

        [DllImport(DLL, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private static extern short mdSend(short path_no, short station_no, short start_io, short size, byte[] buffer);

        // ── Public API ─────────────────────────────────────────────────────────
        /// <summary>보드 오픈. pathNo는 보통 0 (여러 보드면 0,1,...)</summary>
        public static short Open(int pathNo) => mdOpen(CheckedCast(pathNo));

        /// <summary>보드 클로즈.</summary>
        public static short Close(int pathNo) => mdClose(CheckedCast(pathNo));

        /// <summary>
        /// 비트/워드 혼용 가능: 장치코드에 따라 자동 판별하지 않고
        /// MdFun32의 장치 문자열 규칙(X/Y/M/D 등)을 그대로 사용.
        /// 성공 시 on(true/false) 반환.
        /// </summary>
        public static bool TryReadBit(int pathNo, string device, out bool on)
        {
            on = false;
            if (string.IsNullOrWhiteSpace(device)) return false;

            int data = 0;
            short rc = mdDevR(CheckedCast(pathNo), device.Trim(), ref data);
            if (rc != 0) return false;

            on = data != 0;
            return true;
        }

        /// <summary>비트 장치 쓰기: true=1, false=0</summary>
        public static bool TryWriteBit(int pathNo, string device, bool value)
        {
            if (string.IsNullOrWhiteSpace(device)) return false;
            int data = value ? 1 : 0;
            short rc = mdDevSet(CheckedCast(pathNo), device.Trim(), data);
            return rc == 0;
        }

        /// <summary>D 등 워드 장치 읽기 (16bit 정수)</summary>
        public static bool TryReadWord(int pathNo, string device, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(device)) return false;

            short rc = mdDevR(CheckedCast(pathNo), device.Trim(), ref value);
            return rc == 0;
        }

        /// <summary>D 등 워드 장치 쓰기 (16bit 정수)</summary>
        public static bool TryWriteWord(int pathNo, string device, int value)
        {
            if (string.IsNullOrWhiteSpace(device)) return false;

            short rc = mdDevSet(CheckedCast(pathNo), device.Trim(), value);
            return rc == 0;
        }

        // ── Helpers ────────────────────────────────────────────────────────────
        private static short CheckedCast(int pathNo)
        {
            if (pathNo < short.MinValue || pathNo > short.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(pathNo));
            return (short)pathNo;
        }
    }
}
