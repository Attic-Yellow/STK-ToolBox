using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace STK_ToolBox.Helpers
{
    /// <summary>
    /// Mitsubishi CC-Link 보드용 MdFun32(DLL) 래퍼.
    /// 
    /// 주요 기능
    /// - mdopen / mdclose 래핑: 채널(Open/Close) 관리
    /// - IOByteTable_X / IOByteTable_Y 를 LBSControl.db3 에서 읽어 기본 블록 정보 로드
    /// - X/Y 디바이스에 대해 16bit 블록(1워드) 단위 Read/Write
    /// - "X0000", "Y0018", "RX 3.5" 같은 문자열 주소를 DevType/Station/Bit 로 파싱
    /// - 단일 Bit 단위 Read/Write (Y 출력만 쓰기 허용)
    /// 
    /// 설계 의도
    /// - ViewModel/업무 로직에서는 "X01A0", "Y0200" 수준의 문자열만 알고,
    ///   실제 CC-Link 보드 I/O 주소 계산과 MdFun32 호출은 전부 이 래퍼에서 처리.
    /// - 기존 LBS 코드의 GetStationNo / GetIONumber 로직과 동일한 Address→Station/Bit 변환 사용.
    /// - 예외는 최대한 밖으로 던지지 않고 false/0 등의 리턴으로 안정성 확보(장비 통신 실패 시).
    /// </summary>
    public static class MdFunc32Wrapper
    {
        private const string DllName = "MDFUNC32.DLL";

        #region ───────── P/Invoke 선언 ─────────

        [DllImport(DllName, EntryPoint = "mdopen",
            CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        private static extern short mdOpen(short Chan, short mode, ref int path);

        [DllImport(DllName, EntryPoint = "mdclose",
            CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        private static extern short mdClose(int path);

        [DllImport(DllName, EntryPoint = "mdsend",
            CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        private static extern short mdSend(int path, short Stno, short Devtyp, short DevNo,
                                           ref short Size_Renamed, ref short Buf);

        [DllImport(DllName, EntryPoint = "mdreceive",
            CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        private static extern short mdReceive(int path, short Stno, short Devtyp, short DevNo,
                                              ref short Size_Renamed, ref short Buf);

        #endregion

        #region ───────── 상수 / 내부 상태 ─────────

        // X / Y 디바이스 타입 (MdFun32 DevType)
        public const short DevX = 1;
        public const short DevY = 2;

        // 보드 자신의 Station 번호 (기존 LBS 코드의 DEF_CCLINK.STATIONNO 와 동일 개념)
        // 대부분의 예제에서 0xFF 사용
        public const short OwnerStation = 0xFF;

        // mdopen() 에서 받은 path (보드 핸들)
        private static int _path;

        // 현재 열려 있는 채널 번호 (예: 81, 82 ...)
        private static short _channel;

        /// <summary>
        /// MdFun32 보드가 Open 상태인지 여부.
        /// </summary>
        public static bool IsOpened
        {
            get { return _path != 0; }
        }

        /// <summary>
        /// IOByteTable_X / IOByteTable_Y 한 행을 담는 내부 모델.
        /// (현재는 ByteSize만 실사용, StartAddr은 확장용)
        /// </summary>
        private class IoByteInfo
        {
            public short StartAddr;
            public short ByteSize;
        }

        // 채널별 X / Y 정보 (Key = Channel)
        private static readonly Dictionary<short, IoByteInfo> _ioByteX =
            new Dictionary<short, IoByteInfo>();

        private static readonly Dictionary<short, IoByteInfo> _ioByteY =
            new Dictionary<short, IoByteInfo>();

        #endregion

        #region ───────── mdOpen / mdClose 래핑 ─────────

        /// <summary>
        /// 지정한 채널(예: 81)을 Open.
        /// 이미 열려있다면 먼저 Close 후 다시 Open.
        /// </summary>
        public static short Open(short chan)
        {
            // 이미 열려 있으면 먼저 닫고 새로 연다.
            if (_path != 0)
            {
                try { mdClose(_path); }
                catch { /* 보드 쪽 에러는 무시하고 재시도 */ }
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

        /// <summary>
        /// 현재 열려 있는 채널/보드를 Close.
        /// </summary>
        public static short Close()
        {
            if (_path == 0) return 0;

            short rc = mdClose(_path);
            _path = 0;
            _channel = 0;
            return rc;
        }

        #endregion

        #region ───────── IOByteTable 로드 ─────────

        /// <summary>
        /// LBS_DB 의 IOByteTable_X, IOByteTable_Y 를 로드해서
        /// 채널별 기본 StartAddr/ByteSize 정보를 캐시한다.
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

        /// <summary>
        /// 특정 IOByteTable(예: IOByteTable_X)을 읽어서 채널별 IoByteInfo 딕셔너리 구성.
        /// </summary>
        private static void LoadIoByteTable(SQLiteConnection conn, string tableName, Dictionary<short, IoByteInfo> target)
        {
            using (var cmd = new SQLiteCommand(
                "SELECT Channel, StartAddr, ByteSize FROM " + tableName + ";", conn))
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
        /// (현재 구현은 항상 2를 사용하고 있음. 향후 확장 시 info.ByteSize 사용 가능.)
        /// </summary>
        private static short GetBlockByteSize(short devType)
        {
            IoByteInfo info;
            Dictionary<short, IoByteInfo> map = (devType == DevX) ? _ioByteX : _ioByteY;

            if (_channel != 0 && map.TryGetValue(_channel, out info))
            {
                if (info.ByteSize < 2) return 2;
                return 2; // 지금은 1 word(2 byte)만 사용. 필요하면 info.ByteSize로 확장 가능.
            }

            return 2;
        }

        #endregion

        #region ───────── 내부 주소 계산 (Station/Bit ↔ DevNo) ─────────

        /// <summary>
        /// Station(1~)과 블록 시작 비트(0 또는 16)를 CC-Link 로지컬 DevNo 로 변환.
        /// 
        /// 기존 LBS 코드의 GetStationNo / GetIONumber 역함수와 대응:
        ///   station = Addr/32 + 1, bit = Addr % 32  ⇔  Addr = (station-1)*32 + bit
        /// 여기서는 bit 대신 blockStartBit(0 또는 16)를 사용한다.
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

        #region ───────── 16비트 블록 Read/Write ─────────

        /// <summary>
        /// 특정 스테이션에서 16비트(1워드) 블록을 읽는다.
        /// station: 1~
        /// blockStartBit: 0 또는 16 (0~31 중에서 16비트 경계)
        /// devType: DevX(1) 또는 DevY(2)
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
        /// devType: DevX / DevY
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

        #region ───────── 단일 비트 Read/Write ─────────

        /// <summary>
        /// "X0000", "Y0018", "RX 3.5" 와 같은 주소 문자열로부터
        /// 해당 비트의 현재 상태를 읽어온다.
        /// (X/Y 구분, Station/Bit 계산은 TryParseForBlock 사용)
        /// </summary>
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

        /// <summary>
        /// "Y00B0", "RY 2.10" 같은 출력(Y) 주소에 대해 단일 비트를 On/Off 한다.
        /// - DevType 이 Y 가 아닌 경우(false) 바로 실패 처리.
        /// </summary>
        public static bool TryWriteBit(string device, bool value)
        {
            if (_path == 0 || string.IsNullOrWhiteSpace(device)) return false;

            short devType;
            short station;
            int bit;

            if (!TryParseForBlock(device.Trim(), out devType, out station, out bit))
                return false;

            // 출력(Y)만 쓰기 허용
            if (devType != DevY) return false;

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

        #region ───────── 주소 파싱 (문자열 → DevType, Station, Bit) ─────────

        // 형식: "RX 3.5" / "RY 2.10"
        private static readonly Regex RxRy =
            new Regex(@"^\s*R([XY])\s*(\d+)\s*\.\s*(\d+)\s*$",
                      RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // 형식: "X0000" / "Y0018" / "Y00B0" (숫자 부분은 16진수)
        private static readonly Regex Xy =
            new Regex(@"^\s*([XY])\s*([0-9A-Fa-f]+)\s*$",
                      RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// "RX 3.5", "RY 2.10", "X0000", "Y0020" 같은 문자열을
        /// DevType / Station / Bit 로 파싱한다.
        /// 
        /// Station, Bit 계산은 기존 LBS 논리와 동일:
        ///   station = Addr/32 + 1
        ///   bit     = Addr % 32
        /// (여기서 Addr 는 16진수 숫자 부분을 Linear Address 로 해석)
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

            // 2) X0000 / Y0018 / Y00B0 같은 형식: 숫자 부분은 "항상 16진수"로 본다.
            var m2 = Xy.Match(device);
            if (!m2.Success)
                return false;

            char c = char.ToUpperInvariant(m2.Groups[1].Value[0]);
            devType = (short)(c == 'X' ? DevX : DevY);

            string num = m2.Groups[2].Value.Trim();

            int linear;
            try
            {
                // 16진수로 통일
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
