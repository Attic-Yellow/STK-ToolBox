using STK_ToolBox.Helpers;
using STK_ToolBox.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using WinForms = System.Windows.Forms;

namespace STK_ToolBox.ViewModels
{
    public class IOByteSheetViewModel : BaseViewModel
    {
        private const string SettingsFileName = "IOByteSheetSettings.txt";

        public ObservableCollection<IOByteSegment> Segments { get; private set; }

        #region 인버터 설정 프로퍼티

        private int _inverterChannel = 81;
        public int InverterChannel
        {
            get { return _inverterChannel; }
            set
            {
                if (_inverterChannel != value)
                {
                    _inverterChannel = value;
                    OnPropertyChanged();
                    BuildSegments();   // 값 변경 즉시 반영
                }
            }
        }

        private int _stationCount = 8;
        public int StationCount
        {
            get { return _stationCount; }
            set
            {
                if (_stationCount != value)
                {
                    _stationCount = value;
                    OnPropertyChanged();
                    BuildSegments();
                }
            }
        }

        private int _bytesPerStation = 2;
        public int BytesPerStation
        {
            get { return _bytesPerStation; }
            set
            {
                if (_bytesPerStation != value)
                {
                    _bytesPerStation = value;
                    OnPropertyChanged();
                    BuildSegments();
                }
            }
        }

        private string _startAddressHex = "0";
        public string StartAddressHex
        {
            get { return _startAddressHex; }
            set
            {
                if (_startAddressHex != value)
                {
                    _startAddressHex = value;
                    OnPropertyChanged();
                    BuildSegments();
                }
            }
        }

        private bool _useInverterConfig = false;
        public bool UseInverterConfig
        {
            get { return _useInverterConfig; }
            set
            {
                if (_useInverterConfig != value)
                {
                    _useInverterConfig = value;
                    OnPropertyChanged();
                    BuildSegments();
                }
            }
        }

        #endregion

        #region 경로 / 상태

        private string _outputFolder =
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

        public string OutputFolder
        {
            get { return _outputFolder; }
            set
            {
                if (_outputFolder != value)
                {
                    _outputFolder = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _outputFileName = "IOByteSheet";
        public string OutputFileName
        {
            get { return _outputFileName; }
            set
            {
                if (_outputFileName != value)
                {
                    _outputFileName = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _exportStatusMessage;
        public string ExportStatusMessage
        {
            get { return _exportStatusMessage; }
            set
            {
                if (_exportStatusMessage != value)
                {
                    _exportStatusMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _dbStatusMessage;
        public string DbStatusMessage
        {
            get { return _dbStatusMessage; }
            set
            {
                if (_dbStatusMessage != value)
                {
                    _dbStatusMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _settingsStatusMessage;
        public string SettingsStatusMessage
        {
            get { return _settingsStatusMessage; }
            set
            {
                if (_settingsStatusMessage != value)
                {
                    _settingsStatusMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion

        #region Commands

        public ICommand BrowseFolderCommand { get; private set; }
        public ICommand ExportCommand { get; private set; }
        public ICommand RefreshFromDbCommand { get; private set; }
        public ICommand SaveSettingsCommand { get; private set; }
        public ICommand LoadSettingsCommand { get; private set; }
        public ICommand HelpCommand { get; private set; }

        #endregion

        public IOByteSheetViewModel()
        {
            Segments = new ObservableCollection<IOByteSegment>();

            BrowseFolderCommand = new RelayCommand(() => BrowseFolder());
            ExportCommand = new RelayCommand(() => Export(), () => CanExport());
            RefreshFromDbCommand = new RelayCommand(() => RefreshFromDb());
            SaveSettingsCommand = new RelayCommand(() => SaveSettings());
            LoadSettingsCommand = new RelayCommand(() => LoadSettings());
            HelpCommand = new RelayCommand(() => ShowHelp());

            // 초기
            LoadSettings();
            BuildSegments();
        }

        #region 폴더 선택

        private void BrowseFolder()
        {
            using (var dialog = new WinForms.FolderBrowserDialog())
            {
                dialog.SelectedPath = OutputFolder;
                dialog.Description = "IO Byte Sheet 파일을 저장할 폴더를 선택하세요.";

                if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                    OutputFolder = dialog.SelectedPath;
            }
        }

        #endregion

        #region Export

        private bool CanExport()
        {
            return !string.IsNullOrWhiteSpace(OutputFolder)
                   && !string.IsNullOrWhiteSpace(OutputFileName);
        }

        private void Export()
        {
            try
            {
                if (!Directory.Exists(OutputFolder))
                    Directory.CreateDirectory(OutputFolder);

                string filePath = Path.Combine(OutputFolder, OutputFileName + ".xlsx");

                // 화면에 보이는 Segments 그대로 엑셀로
                ExcelExporter.IoByteSheetExport(Segments, filePath);

                ExportStatusMessage = "엑셀 생성 완료: " + filePath;
            }
            catch (Exception ex)
            {
                ExportStatusMessage = "엑셀 생성 실패: " + ex.Message;
                MessageBox.Show(ExportStatusMessage, "IO Byte Sheet",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region 도움말

        private void ShowHelp()
        {
            var sb = new StringBuilder();

            sb.AppendLine("【 IO Byte Sheet 사용 안내 】");
            sb.AppendLine();
            sb.AppendLine("1. 사용 전제조건");
            sb.AppendLine("- D:\\LBS_DB\\LBSControl.db3 의 IOMonitoring 테이블이 완성되어 있어야 합니다.");
            sb.AppendLine("  (Address, Channel 컬럼에 X/Y IO 정보가 모두 등록된 상태)");
            sb.AppendLine("- Address 는 X / Y 디바이스 주소만 대상입니다. (예: X01A0, Y0200)");
            sb.AppendLine();
            sb.AppendLine("2. 인버터 세그먼트 제약사항");
            sb.AppendLine("- 인버터 Station 은 연속된 구간으로만 생성됩니다.");
            sb.AppendLine("- 중간 Station 을 건너뛰거나, 특정 Station 만 선택해서 제외하는 기능은 지원하지 않습니다.");
            sb.AppendLine("  (예: Station 1,2,3,4 중 2번만 빼고 생성하는 기능은 없음)");
            sb.AppendLine();
            sb.AppendLine("3. 기본 사용 방법");
            sb.AppendLine("- IOMonitoring 테이블을 먼저 완성합니다.");
            sb.AppendLine("- [인버터 설정] 구간에서 Channel / Station 수 / Byte/Station / 시작 Addr(HEX)를 입력합니다.");
            sb.AppendLine("- \"인버터 설정 사용\" 체크 시, 위 설정에 따라 X/Y 인버터 세그먼트가 자동 추가됩니다.");
            sb.AppendLine("- 상단 DB 상태에서 [IOMonitoring 다시 읽기] 버튼으로 DB 기준 세그먼트를 갱신할 수 있습니다.");
            sb.AppendLine("- \"IO Byte Sheet 엑셀 생성\" 버튼을 누르면 X/Y 각각 IOByteTable_X / IOByteTable_Y 시트로 내보냅니다.");
            sb.AppendLine();
            sb.AppendLine("※ StartAddr, Station 설정을 바꾸면 화면에 즉시 반영되며,");
            sb.AppendLine("   엑셀에서는 StartAddr 열이 텍스트 형식으로 저장되어 3E0, 4E0 등이 그대로 표시됩니다.");

            MessageBox.Show(sb.ToString(),
                "IO Byte Sheet 도움말",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        #endregion

        #region Hex 파싱 + 정렬 도우미

        private static int ParseHex(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
                return 0;

            int value;
            if (!int.TryParse(hex.Trim(),
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out value))
            {
                return 0;
            }
            return value;
        }

        /// <summary>
        /// IO 순번(채널 → Device(X/Y) → StartAddress(HEX)) 기준 정렬용 비교 함수
        /// </summary>
        private static int CompareSegments(IOByteSegment a, IOByteSegment b)
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a == null) return -1;
            if (b == null) return 1;

            int chCmp = a.Channel.CompareTo(b.Channel);
            if (chCmp != 0) return chCmp;

            // X 먼저, 그 다음 Y (대소문자 무시)
            int devCmp = string.Compare(a.Device, b.Device, StringComparison.OrdinalIgnoreCase);
            if (devCmp != 0) return devCmp;

            int addrA = ParseHex(a.StartAddressHex);
            int addrB = ParseHex(b.StartAddressHex);
            int addrCmp = addrA.CompareTo(addrB);
            if (addrCmp != 0) return addrCmp;

            // 마지막 tie-breaker: Source
            return string.Compare(a.Source, b.Source, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Segments 구성 (IOMonitoring + Inverter)

        private void BuildSegments()
        {
            // 1) DB 기반 세그먼트
            var list = new List<IOByteSegment>();
            var dbSegments = LoadSegmentsFromIOMonitoring();
            foreach (var seg in dbSegments)
                list.Add(seg);

            // 2) 인버터 설정 사용 시, X/Y 둘 다 추가
            if (UseInverterConfig)
            {
                var invSegs = BuildInverterSegments();
                foreach (var s in invSegs)
                    list.Add(s);
            }

            // 3) IO 순번(채널 → Device → StartAddr) 기준 정렬
            list.Sort(CompareSegments);

            // 4) ObservableCollection 갱신
            Segments.Clear();
            foreach (var seg in list)
                Segments.Add(seg);
        }

        /// <summary>
        /// 인버터 설정값으로부터 X/Y 구간 모두 생성.
        /// </summary>
        private ObservableCollection<IOByteSegment> BuildInverterSegments()
        {
            var result = new ObservableCollection<IOByteSegment>();

            // 입력 중이거나 잘못된 HEX 값일 때는 인버터 세그먼트만 생성하지 않고 조용히 리턴
            int startAddrDec;
            if (!int.TryParse(StartAddressHex ?? "0",
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out startAddrDec))
            {
                return result;
            }

            for (int i = 0; i < StationCount; i++)
            {
                int stationNo = i + 1;

                // 기존 로직 유지: 0x20 간격
                int addr = startAddrDec + (stationNo - 1) * 0x20;
                string hex = addr.ToString("X");

                // X 구간
                result.Add(new IOByteSegment
                {
                    Channel = InverterChannel,
                    Device = "X",
                    StartAddressHex = hex,
                    ByteSize = BytesPerStation,
                    Source = "Inverter"
                });

                // Y 구간
                result.Add(new IOByteSegment
                {
                    Channel = InverterChannel,
                    Device = "Y",
                    StartAddressHex = hex,
                    ByteSize = BytesPerStation,
                    Source = "Inverter"
                });
            }

            return result;
        }

        #endregion

        #region IOMonitoring 읽기 + Byte 묶기

        private void RefreshFromDb()
        {
            try
            {
                DbStatusMessage = "IOMonitoring 재읽기 중...";
                BuildSegments();
                DbStatusMessage = "IOMonitoring 읽기 완료.";
            }
            catch (Exception ex)
            {
                DbStatusMessage = "IOMonitoring 읽기 실패: " + ex.Message;
            }
        }

        private ObservableCollection<IOByteSegment> LoadSegmentsFromIOMonitoring()
        {
            var result = new ObservableCollection<IOByteSegment>();

            try
            {
                string dbPath = @"D:\LBS_DB\LBSControl.db3";
                if (!File.Exists(dbPath))
                {
                    DbStatusMessage = "DB 파일을 찾을 수 없습니다: " + dbPath;
                    return result;
                }

                string connStr = "Data Source=" + dbPath + ";Version=3;";

                var counter = new Dictionary<string, int>(); // key: "Channel|Device|BaseHex"

                using (var conn = new SQLiteConnection(connStr))
                using (var cmd = conn.CreateCommand())
                {
                    conn.Open();
                    cmd.CommandText = @"
                        SELECT Address, Channel
                        FROM   IOMonitoring
                        WHERE  TRIM(Address) <> '' AND TRIM(Channel) <> ''
                    ";

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string addrRaw = Convert.ToString(reader["Address"]);
                            string chStr = Convert.ToString(reader["Channel"]);

                            if (string.IsNullOrWhiteSpace(addrRaw) ||
                                string.IsNullOrWhiteSpace(chStr))
                                continue;

                            addrRaw = addrRaw.Trim().ToUpper();   // X01A0
                            chStr = chStr.Trim();

                            char dev = addrRaw[0];
                            if (dev != 'X' && dev != 'Y')
                                continue;

                            string hexPart = addrRaw.Substring(1); // 01A0

                            int offset;
                            if (!int.TryParse(hexPart, System.Globalization.NumberStyles.HexNumber,
                                System.Globalization.CultureInfo.InvariantCulture, out offset))
                                continue;

                            int channel;
                            if (!int.TryParse(chStr, out channel))
                                continue;

                            int baseAddr = offset & ~0xF;     // 16비트(0x10) 단위
                            string baseHex = baseAddr.ToString("X");

                            string key = channel + "|" + dev + "|" + baseHex;

                            int count;
                            counter.TryGetValue(key, out count);
                            counter[key] = count + 1;
                        }
                    }
                }

                foreach (var kvp in counter)
                {
                    string[] parts = kvp.Key.Split('|');
                    if (parts.Length != 3) continue;

                    int channel;
                    if (!int.TryParse(parts[0], out channel))
                        continue;

                    string device = parts[1];
                    string baseHex = parts[2];
                    int bitCount = kvp.Value;

                    int byteSize = (bitCount + 7) / 8;

                    result.Add(new IOByteSegment
                    {
                        Channel = channel,
                        Device = device,
                        StartAddressHex = baseHex,
                        ByteSize = byteSize,
                        Source = "IOMonitoring"
                    });
                }

                DbStatusMessage = string.Format(
                    "IOMonitoring 읽기 완료. (DB: {0}, Segment: {1}개)",
                    Path.GetFileName(dbPath), result.Count);
            }
            catch (Exception ex)
            {
                DbStatusMessage = "IOMonitoring 읽기 중 오류: " + ex.Message;
            }

            return result;
        }

        #endregion

        #region 설정 저장/불러오기

        private void SaveSettings()
        {
            try
            {
                string baseFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "STK_ToolBox");

                Directory.CreateDirectory(baseFolder);
                string path = Path.Combine(baseFolder, SettingsFileName);

                var sb = new StringBuilder();
                sb.AppendLine("InverterChannel=" + InverterChannel);
                sb.AppendLine("StationCount=" + StationCount);
                sb.AppendLine("BytesPerStation=" + BytesPerStation);
                sb.AppendLine("StartAddressHex=" + StartAddressHex);
                sb.AppendLine("UseInverterConfig=" + UseInverterConfig);
                sb.AppendLine("OutputFolder=" + OutputFolder);
                sb.AppendLine("OutputFileName=" + OutputFileName);

                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
                SettingsStatusMessage = "설정을 저장했습니다.";
            }
            catch (Exception ex)
            {
                SettingsStatusMessage = "설정 저장 실패: " + ex.Message;
            }
        }

        private void LoadSettings()
        {
            try
            {
                string baseFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "STK_ToolBox");

                string path = Path.Combine(baseFolder, SettingsFileName);
                if (!File.Exists(path))
                {
                    SettingsStatusMessage = "저장된 설정이 없습니다.";
                    return;
                }

                var lines = File.ReadAllLines(path, Encoding.UTF8);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    int idx = line.IndexOf('=');
                    if (idx <= 0) continue;

                    string key = line.Substring(0, idx).Trim();
                    string val = line.Substring(idx + 1).Trim();

                    switch (key)
                    {
                        case "InverterChannel":
                            int.TryParse(val, out _inverterChannel);
                            break;
                        case "StationCount":
                            int.TryParse(val, out _stationCount);
                            break;
                        case "BytesPerStation":
                            int.TryParse(val, out _bytesPerStation);
                            break;
                        case "StartAddressHex":
                            _startAddressHex = val;
                            break;
                        case "UseInverterConfig":
                            bool b;
                            if (bool.TryParse(val, out b))
                                _useInverterConfig = b;
                            break;
                        case "OutputFolder":
                            if (!string.IsNullOrWhiteSpace(val))
                                _outputFolder = val;
                            break;
                        case "OutputFileName":
                            if (!string.IsNullOrWhiteSpace(val))
                                _outputFileName = val;
                            break;
                    }
                }

                OnPropertyChanged(nameof(InverterChannel));
                OnPropertyChanged(nameof(StationCount));
                OnPropertyChanged(nameof(BytesPerStation));
                OnPropertyChanged(nameof(StartAddressHex));
                OnPropertyChanged(nameof(UseInverterConfig));
                OnPropertyChanged(nameof(OutputFolder));
                OnPropertyChanged(nameof(OutputFileName));

                SettingsStatusMessage = "설정을 불러왔습니다.";
            }
            catch (Exception ex)
            {
                SettingsStatusMessage = "설정 불러오기 실패: " + ex.Message;
            }
        }

        #endregion
    }
}
