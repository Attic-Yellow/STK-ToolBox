using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace STK_ToolBox.Helpers
{
    public static class MdFunc32Wrapper
    {
        private const string DllName = "MDFUNC32.DLL";

        #region P/Invoke

        [DllImport(DllName, EntryPoint = "mdopen", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        private static extern short mdOpen(short Chan, short mode, ref int path);

        [DllImport(DllName, EntryPoint = "mdclose", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        private static extern short mdClose(int path);

        [DllImport(DllName, EntryPoint = "mdsend", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        private static extern short mdSend(int path, short Stno, short Devtyp, short DevNo, ref short Size_Renamed, ref short Buf);

        [DllImport(DllName, EntryPoint = "mdreceive", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        private static extern short mdReceive(int path, short Stno, short Devtyp, short DevNo, ref short Size_Renamed, ref short Buf);

        #endregion

        // X / Y 디바이스 타입
        public const short DevX = 1;
        public const short DevY = 2;

        // 보드 자신의 Station 번호 (기존 LBS 코드의 DEF_CCLINK.STATIONNO 와 동일 개념)
        // 대부분의 예제에서 0xFF 사용
        public const short OwnerStation = 0xFF;

        // mdopen() 에서 받은 path
        private static int _path;
        // 현재 열려 있는 채널(81, 82 ...)
        private static short _channel;

        public static bool IsOpened
        {
            get { return _path != 0; }
        }

        /// <summary>
        /// IOByteTable_X / IOByteTable_Y 의 한 행 정보
        /// (지금은 ByteSize만 사실상 사용, StartAddr은 필요 시 확장용)
        /// </summary>
        private class IoByteInfo
        {
            public short StartAddr;
            public short ByteSize;
        }

        // 채널별 X / Y 정보 (Key = Channel)
        private static readonly Dictionary<short, IoByteInfo> _ioByteX = new Dictionary<short, IoByteInfo>();
        private static readonly Dictionary<short, IoByteInfo> _ioByteY = new Dictionary<short, IoByteInfo>();

        #region mdOpen / mdClose

        public static short Open(short chan)
        {
            // 이미 열려 있으면 먼저 닫고 새로 연다.
            if (_path != 0)
            {
                try { mdClose(_path); }
                catch { }
                _path = 0;
            }

            short rc = mdOpen(chan, -1, ref _path);
            if (rc != 0)
            {
                _path = 0;
                _channel = 0;
                return rc;
            }

            _channel = chan;
            return rc;
        }

        public static short Close()
        {
            if (_path == 0) return 0;

            short rc = mdClose(_path);
            _path = 0;
            _channel = 0;
            return rc;
        }

        #endregion

        #region IOByteTable 로드

        /// <summary>
        /// LBS_DB 의 IOByteTable_X, IOByteTable_Y 를 로드한다.
        /// DbPath 예: D:\LBS_DB\LBSControl.db3
        /// </summary>
        public static void LoadIoByteTables(string dbPath)
        {
            _ioByteX.Clear();
            _ioByteY.Clear();

            if (string.IsNullOrWhiteSpace(dbPath))
                return;

            if (!System.IO.File.Exists(dbPath))
                return;

            using (var conn = new SQLiteConnection("Data Source=" + dbPath + ";Version=3;"))
            {
                conn.Open();

                LoadIoByteTable(conn, "IOByteTable_X", _ioByteX);
                LoadIoByteTable(conn, "IOByteTable_Y", _ioByteY);
            }
        }

        private static void LoadIoByteTable(SQLiteConnection conn, string tableName, Dictionary<short, IoByteInfo> target)
        {
            using (var cmd = new SQLiteCommand("SELECT Channel, StartAddr, ByteSize FROM " + tableName + ";", conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    string chText = Convert.ToString(reader["Channel"]) ?? "0";
                    string addrText = Convert.ToString(reader["StartAddr"]) ?? "0";
                    string sizeText = Convert.ToString(reader["ByteSize"]) ?? "0";

                    short channel;
                    short startAddr;
                    short byteSize;

                    if (!short.TryParse(chText, NumberStyles.Integer, CultureInfo.InvariantCulture, out channel))
                        continue;
                    if (!short.TryParse(addrText, NumberStyles.Integer, CultureInfo.InvariantCulture, out startAddr))
                        startAddr = 0;
                    if (!short.TryParse(sizeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out byteSize))
                        byteSize = 2;

                    target[channel] = new IoByteInfo
                    {
                        StartAddr = startAddr,
                        ByteSize = byteSize
                    };
                }
            }
        }

        /// <summary>
        /// 현재 채널과 DevType에 대해 기본 블록 Size(바이트)를 돌려준다.
        /// 지금 구조에서는 1워드(2바이트)만 쓰므로 최소 2 반환.
        /// IOByteTable_* 에 값이 있으면 그 값을 사용하되 2보다 작으면 2로 올림.
        /// </summary>
        private static short GetBlockByteSize(short devType)
        {
            IoByteInfo info;
            Dictionary<short, IoByteInfo> map = (devType == DevX) ? _ioByteX : _ioByteY;

            if (_channel != 0 && map.TryGetValue(_channel, out info))
            {
                if (info.ByteSize < 2) return 2;
                return 2; // ★ 지금은 1 word(2 byte)만 사용. 필요하면 info.ByteSize로 확장 가능.
            }

            return 2;
        }

        #endregion

        #region 내부 주소 계산

        /// <summary>
        /// Station(1~)과 블록 시작 비트(0 또는 16)를 CC-Link 로지컬 DevNo 로 변환.
        /// 기존 LBS 코드의 GetStationNo / GetIONumber 역함수:
        ///   station = Addr/32 + 1, bit = Addr % 32
        /// → Addr = (station-1)*32 + bit
        /// 여기서 bit 대신 blockStartBit(0 또는 16)를 사용한다.
        /// </summary>
        private static short ToLogicalAddr(short station, short blockStartBit)
        {
            if (station <= 0) station = 1;
            if (blockStartBit < 0) blockStartBit = 0;
            if (blockStartBit > 31) blockStartBit = 16; // 0 또는 16만 예상

            int addr = (station - 1) * 32 + blockStartBit;
            if (addr < short.MinValue) addr = short.MinValue;
            if (addr > short.MaxValue) addr = short.MaxValue;

            return (short)addr;
        }

        #endregion

        #region 16비트 블록 Read/Write

        /// <summary>
        /// 특정 스테이션에서 16비트(1워드) 블록을 읽는다.
        /// station: 1~
        /// blockStartBit: 0 또는 16 (0~31 중에서 16비트 경계)
        /// </summary>
        public static bool TryReadBlock16(short station, short devType, short blockStartBit, out ushort bits)
        {
            bits = 0;
            if (_path == 0) return false;

            short size = 2; // 1 word = 2 bytes
            short buf = 0;

            short devNo = ToLogicalAddr(station, blockStartBit);

            // Stno 자리에는 OwnerStation(보드 자신) 사용
            short rc = mdReceive(_path, OwnerStation, devType, devNo, ref size, ref buf);
            if (rc != 0) return false;

            bits = (ushort)buf;
            return true;
        }

        /// <summary>
        /// 특정 스테이션에서 16비트(1워드) 블록을 쓴다.
        /// </summary>
        public static bool TryWriteBlock16(short station, short devType, short blockStartBit, ushort bits)
        {
            if (_path == 0) return false;

            short size = 2;
            short buf = (short)bits;

            short devNo = ToLogicalAddr(station, blockStartBit);

            short rc = mdSend(_path, OwnerStation, devType, devNo, ref size, ref buf);
            return rc == 0;
        }

        #endregion

        #region 단일 비트 Read/Write

        public static bool TryReadBit(string device, out bool on)
        {
            on = false;
            if (_path == 0 || string.IsNullOrWhiteSpace(device)) return false;

            short devType;
            short station;
            int bit;

            if (!TryParseForBlock(device.Trim(), out devType, out station, out bit))
                return false;

            short blockStart = (short)((bit / 16) * 16);
            int offset = bit % 16;

            ushort bits;
            if (!TryReadBlock16(station, devType, blockStart, out bits)) return false;

            on = ((bits >> offset) & 1) != 0;
            return true;
        }

        public static bool TryWriteBit(string device, bool value)
        {
            if (_path == 0 || string.IsNullOrWhiteSpace(device)) return false;

            short devType;
            short station;
            int bit;

            if (!TryParseForBlock(device.Trim(), out devType, out station, out bit))
                return false;

            if (devType != DevY) return false; // Y(출력)만 쓴다.

            short blockStart = (short)((bit / 16) * 16);
            int offset = bit % 16;

            ushort bits;
            if (!TryReadBlock16(station, devType, blockStart, out bits))
                bits = 0;

            if (value)
                bits = (ushort)(bits | (1 << offset));
            else
                bits = (ushort)(bits & ~(1 << offset));

            return TryWriteBlock16(station, devType, blockStart, bits);
        }

        #endregion

        #region 주소 파싱 (X/Y 문자열 → DevType, Station, Bit)

        private static readonly Regex RxRy =
            new Regex(@"^\s*R([XY])\s*(\d+)\s*\.\s*(\d+)\s*$",
                      RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex Xy =
            new Regex(@"^\s*([XY])\s*([0-9A-Fa-f]+)\s*$",
                      RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// "RX 3.5", "RY 2.10", "X0000", "Y0020" 같은 문자열을
        /// DevType / Station / Bit 로 파싱한다.
        /// Station, Bit 계산은 기존 LBS 논리와 동일:
        ///   station = linear/32 + 1, bit = linear%32
        /// </summary>
        public static bool TryParseForBlock(string device, out short devType, out short station, out int bit)
        {
            devType = 0;
            station = 0;
            bit = 0;

            if (string.IsNullOrWhiteSpace(device))
                return false;

            // 1) RX 3.5 / RY 2.10 형식 그대로 지원
            var m1 = RxRy.Match(device);
            if (m1.Success)
            {
                char ch = char.ToUpperInvariant(m1.Groups[1].Value[0]);
                devType = (short)(ch == 'X' ? DevX : DevY);

                station = short.Parse(m1.Groups[2].Value, CultureInfo.InvariantCulture);
                bit = int.Parse(m1.Groups[3].Value, CultureInfo.InvariantCulture);

                return (station > 0 && bit >= 0 && bit < 32);
            }

            // 2) X0000 / Y0018 / Y00B0 같은 형식: 숫자 부분은 "항상 16진수"로 본다
            var m2 = Xy.Match(device);
            if (!m2.Success)
                return false;

            char c = char.ToUpperInvariant(m2.Groups[1].Value[0]);
            devType = (short)(c == 'X' ? DevX : DevY);

            string num = m2.Groups[2].Value.Trim();

            int linear;
            try
            {
                // ★ 여기! 16진수로 통일
                linear = int.Parse(num, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }
            catch
            {
                return false;
            }

            if (linear < 0)
                return false;

            // 기존 GetStationNo / GetIONumber 역함수:
            // station = Addr/32 + 1, bit = Addr%32
            station = (short)(linear / 32 + 1);
            bit = linear % 32;

            if (station <= 0)
                station = 1;

            return true;
        }

        #endregion
    }
}
