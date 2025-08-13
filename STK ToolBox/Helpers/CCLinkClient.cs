using System;

namespace STK_ToolBox.Helpers
{
    public interface ICCLinkClient : IDisposable
    {
        bool IsOpen { get; }
        short BoardNo { get; }
        bool Open(short boardNo, out string error);
        void Close();
        short ReadWord(short station, short headAddr, out ushort value, out string error);
        short WriteWord(short station, short headAddr, ushort value, out string error);
        short ReadWords(short station, short headAddr, ushort[] buffer, out string error);
        short WriteWords(short station, short headAddr, ushort[] buffer, out string error);
    }

    public sealed class CCLinkClient : ICCLinkClient
    {
        public bool IsOpen { get; private set; }
        public short BoardNo { get; private set; }

        public bool Open(short boardNo, out string error)
        {
            try
            {
                error = null;
                var rc = CCLinkNative.CCLnkOpen(boardNo);
                if (rc == 0) { IsOpen = true; BoardNo = boardNo; return true; }

                var msg = $"Board open failed (rc={rc}, board={boardNo})";
                Log.Warn(msg);
                error = "BOARD OPEN FAIL"; // 짧게
                return false;
            }
            catch (DllNotFoundException ex)
            {
                Log.Error(ex, $"Open(board={boardNo}) -> DLL not found");
                error = "DLL LOAD FAIL";
                return false;
            }
            catch (BadImageFormatException ex)
            {
                Log.Error(ex, $"Open(board={boardNo}) -> arch mismatch");
                error = "ARCH MISMATCH (x86 ONLY)";
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Open(board={boardNo}) -> unexpected");
                error = "BOARD OPEN FAIL";
                return false;
            }
        }

        public void Close()
        {
            try { if (IsOpen) CCLinkNative.CCLnkClose(BoardNo); }
            catch { }
            IsOpen = false;
        }

        public short ReadWord(short station, short headAddr, out ushort value, out string error)
        {
            value = 0; error = null;
            try
            {
                var buf = new ushort[1];
                var rc = CCLinkNative.ReadRWw(BoardNo, station, headAddr, 1, buf);
                if (rc == 0) { value = buf[0]; return 0; }
                Log.Warn($"ReadRWw rc={rc} (board={BoardNo}, st={station}, addr={headAddr})");
                error = "READ FAIL";
                return rc;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"ReadWord(st={station}, addr={headAddr})");
                error = "READ FAIL";
                return -1;
            }
        }

        public short WriteWord(short station, short headAddr, ushort value, out string error)
        {
            error = null;
            try
            {
                var rc = CCLinkNative.WriteRWr(BoardNo, station, headAddr, 1, new ushort[] { value });
                if (rc == 0) return 0;
                Log.Warn($"WriteRWr rc={rc} (board={BoardNo}, st={station}, addr={headAddr}, val={value})");
                error = "WRITE FAIL";
                return rc;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"WriteWord(st={station}, addr={headAddr}, val={value})");
                error = "WRITE FAIL";
                return -1;
            }
        }

        public short ReadWords(short station, short headAddr, ushort[] buffer, out string error)
        {
            error = null;
            var rc = CCLinkNative.ReadRWw(BoardNo, station, headAddr, (short)buffer.Length, buffer);
            if (rc != 0) error = "ReadRWw rc=" + rc;
            return rc;
        }

        public short WriteWords(short station, short headAddr, ushort[] buffer, out string error)
        {
            error = null;
            var rc = CCLinkNative.WriteRWr(BoardNo, station, headAddr, (short)buffer.Length, buffer);
            if (rc != 0) error = "WriteRWr rc=" + rc;
            return rc;
        }

        public void Dispose() => Close();
    }
}
