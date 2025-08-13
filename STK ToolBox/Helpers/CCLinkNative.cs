using System.Runtime.InteropServices;

namespace STK_ToolBox.Helpers
{
    public static class CCLinkNative
    {
        private const string Dll = "CCLnkIF.dll";

        static CCLinkNative()
        {
            // D:\STK ToolBox 우선 검색
            NativeDllHelper.EnsureLoaded(Dll, @"D:\STK ToolBox", @"D:\STK ToolBox\Bin");
        }

        [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
        public static extern short CCLnkOpen(short boardNo);

        [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
        public static extern short CCLnkClose(short boardNo);

        [DllImport(Dll, CallingConvention = CallingConvention.StdCall, EntryPoint = "CCLnkWriteRWr")]
        public static extern short WriteRWr(short boardNo, short stationNo, short headAddr, short points, ushort[] data);

        [DllImport(Dll, CallingConvention = CallingConvention.StdCall, EntryPoint = "CCLnkReadRWw")]
        public static extern short ReadRWw(short boardNo, short stationNo, short headAddr, short points, ushort[] data);

        [DllImport(Dll, CallingConvention = CallingConvention.StdCall, EntryPoint = "CCLnkWriteRY")]
        public static extern short WriteRY(short boardNo, short stationNo, short headAddr, short points, byte[] bits);

        [DllImport(Dll, CallingConvention = CallingConvention.StdCall, EntryPoint = "CCLnkReadRX")]
        public static extern short ReadRX(short boardNo, short stationNo, short headAddr, short points, byte[] bits);
    }
}
