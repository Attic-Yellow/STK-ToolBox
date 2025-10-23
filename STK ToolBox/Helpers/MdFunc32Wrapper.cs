// Helpers/MdFunc32Wrapper.cs
using System;
using System.Runtime.InteropServices;

namespace STK_ToolBox.Helpers
{
    public static class MdFunc32Wrapper
    {
        // 대부분 stdcall + 32bit DLL. x86로 빌드 권장.
        [DllImport("MdFunc32.dll", EntryPoint = "mdOpen",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int Open(int station);

        [DllImport("MdFunc32.dll", EntryPoint = "mdClose",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int Close(int station);

        // ★ 비트 읽기: device 예) "X000A", "Y0010"
        [DllImport("MdFunc32.dll", EntryPoint = "mdDevRBit",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern int DevRBit(int station,
            [MarshalAs(UnmanagedType.LPStr)] string device,
            short size,
            out ushort data);

        // ★ 비트 쓰기
        [DllImport("MdFunc32.dll", EntryPoint = "mdDevWBit",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern int DevWBit(int station,
            [MarshalAs(UnmanagedType.LPStr)] string device,
            short size,
            ref ushort data);

        public static bool TryReadBit(int station, string device, out bool on)
        {
            on = false;
            if (string.IsNullOrWhiteSpace(device)) return false;

            ushort w;
            var rc = DevRBit(station, device, 1, out w); // 1 bit
            if (rc != 0) return false;

            on = (w & 0x0001) != 0;
            return true;
        }

        public static bool TryWriteBit(int station, string device, bool on)
        {
            ushort w = (ushort)(on ? 1 : 0);
            var rc = DevWBit(station, device, 1, ref w); // 1 bit
            return rc == 0;
        }
    }
}
