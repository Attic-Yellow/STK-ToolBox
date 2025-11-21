using STK_ToolBox.Helpers;
using STK_ToolBox.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Windows.Input;

namespace STK_ToolBox.ViewModels
{
    public class InverterViewModel : IoMonitorViewModelBase
    {
        private readonly string _dbPath = @"D:\LBS_DB\LBSControl.db3";

        public ICommand RefreshCommand { get; private set; }
        public ICommand HelpCommand { get; private set; }

        // 일괄 제어용 커맨드
        public ICommand AllForwardHighToggleCommand { get; private set; }
        public ICommand AllReverseHighToggleCommand { get; private set; }
        public ICommand AllForwardLowToggleCommand { get; private set; }
        public ICommand AllReverseLowToggleCommand { get; private set; }

        // 버튼 상태 표시용 플래그 (ON/OFF 텍스트 결정)
        private bool _allForwardHighOn;
        public bool AllForwardHighOn
        {
            get { return _allForwardHighOn; }
            set
            {
                if (_allForwardHighOn != value)
                {
                    _allForwardHighOn = value;
                    OnPropertyChanged("AllForwardHighOn");
                }
            }
        }

        private bool _allReverseHighOn;
        public bool AllReverseHighOn
        {
            get { return _allReverseHighOn; }
            set
            {
                if (_allReverseHighOn != value)
                {
                    _allReverseHighOn = value;
                    OnPropertyChanged("AllReverseHighOn");
                }
            }
        }

        private bool _allForwardLowOn;
        public bool AllForwardLowOn
        {
            get { return _allForwardLowOn; }
            set
            {
                if (_allForwardLowOn != value)
                {
                    _allForwardLowOn = value;
                    OnPropertyChanged("AllForwardLowOn");
                }
            }
        }

        private bool _allReverseLowOn;
        public bool AllReverseLowOn
        {
            get { return _allReverseLowOn; }
            set
            {
                if (_allReverseLowOn != value)
                {
                    _allReverseLowOn = value;
                    OnPropertyChanged("AllReverseLowOn");
                }
            }
        }

        public InverterViewModel()
            : base(81, "inverter_state.csv")
        {
            // IOByteTable_X/Y 정보 로드 (블록 범위 체크에 사용)
            MdFunc32Wrapper.LoadIoByteTables(_dbPath);

            RefreshCommand = new RelayCommand(new Action(LoadInverterList));
            HelpCommand = new RelayCommand(new Action(ShowHelp));

            // 일괄 제어 커맨드 등록
            AllForwardHighToggleCommand = new RelayCommand(new Action(ToggleAllForwardHigh));
            AllReverseHighToggleCommand = new RelayCommand(new Action(ToggleAllReverseHigh));
            AllForwardLowToggleCommand = new RelayCommand(new Action(ToggleAllForwardLow));
            AllReverseLowToggleCommand = new RelayCommand(new Action(ToggleAllReverseLow));

            LoadInverterList();
        }

        /// <summary>
        /// 인버터 화면은 전체 IOList를 모니터링
        /// </summary>
        protected override IEnumerable<IOMonitorItem> GetVisibleItems()
        {
            return IOList;
        }

        private void LoadInverterList()
        {
            IOList.Clear();

            if (!File.Exists(_dbPath))
            {
                PostPopup("SQLite DB 파일을 찾을 수 없습니다:\r\n" + _dbPath,
                          "DB 오류",
                          System.Windows.MessageBoxImage.Error);
                return;
            }

            try
            {
                using (var conn = new SQLiteConnection("Data Source=" + _dbPath + ";Version=3;"))
                {
                    conn.Open();

                    using (var cmd = new SQLiteCommand("SELECT * FROM LBS_Conv;", conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            // CONV ID (CONV1, CONV2 ...)
                            string convId = SafeRead(reader, "ID");

                            // 인버터 스테이션 번호
                            string stStr = SafeRead(reader, "InvStationNo");

                            // CnvDirection: 12 = IN, 6 = OUT
                            string dirRaw = SafeRead(reader, "cnvDirection");
                            if (string.IsNullOrWhiteSpace(dirRaw))
                                dirRaw = SafeRead(reader, "CnvDirection"); // 혹시 대소문자 다른 경우 대비

                            int stationNo;
                            if (!int.TryParse(stStr, out stationNo) || stationNo <= 0)
                                continue;

                            string dirLabel = "N/A";
                            if (dirRaw == "12")
                                dirLabel = "IN";
                            else if (dirRaw == "6")
                                dirLabel = "OUT";

                            // CONV 헤더/그룹에 표시할 문자열
                            // 예: "CONV1 (IN, ST#31)"
                            string unitLabel = string.Format("{0} ({1}, ST#{2})", convId, dirLabel, stationNo);

                            // 각 스테이션별 4개 기능 생성
                            IOList.Add(MakeInverterItem(unitLabel, stationNo, "정방향", 0));
                            IOList.Add(MakeInverterItem(unitLabel, stationNo, "역방향", 1));
                            IOList.Add(MakeInverterItem(unitLabel, stationNo, "고속", 3));
                            IOList.Add(MakeInverterItem(unitLabel, stationNo, "저속", 4));
                        }
                    }
                }

                BuildAddressCache();
                LoadSavedStates(true);
                var _ = PollVisibleItemsAsync();

                // 일괄 버튼 상태 초기화 (필요시 나중에 하드웨어 읽어와서 맞춰도 됨)
                AllForwardHighOn = false;
                AllReverseHighOn = false;
                AllForwardLowOn = false;
                AllReverseLowOn = false;
            }
            catch (Exception ex)
            {
                PostPopup("로드 오류: " + ex.Message,
                          "오류",
                          System.Windows.MessageBoxImage.Error);
            }
        }

        private IOMonitorItem MakeInverterItem(string unitLabel, int stationNo, string mode, int bitNo)
        {
            // CC-Link linear 주소 계산: (Station-1)*32 + bit
            int linear = (stationNo - 1) * 32 + bitNo;

            // 항상 4자리 16진수 (예: 0 → 0000, 10 → 000A)
            string hex = linear.ToString("X4");
            string addr = "Y" + hex;

            return new IOMonitorItem
            {
                Id = 0,
                Unit = unitLabel,          // CONV + 방향 + 스테이션 예) CONV1 (IN, ST#31)
                IOName = mode,               // 정방향 / 역방향 / 저속 / 고속
                Address = addr,               // Y0000 형식
                DetailUnit = "Inverter",
                Description = mode + " 출력",
                CurrentState = false
            };
        }

        private string SafeRead(SQLiteDataReader reader, string name)
        {
            try
            {
                int idx = reader.GetOrdinal(name);
                if (idx >= 0 && !reader.IsDBNull(idx))
                    return reader.GetValue(idx).ToString();
            }
            catch
            {
                // ignore
            }
            return "";
        }

        /// <summary>
        /// 인버터에서는 정방향(bit 0), 역방향(bit 1), 고속(bit 3), 저속(bit 4) 모두 토글 가능
        /// </summary>
        protected override bool CanToggleOutput(IOMonitorItem item)
        {
            if (!base.CanToggleOutput(item))
                return false;

            ParsedAddr p;
            if (!_addrCache.TryGetValue(item, out p))
                return false;

            // Y 출력이면서 bit 0~4 모두 허용
            return p.IsOutput && (p.Bit >= 0 && p.Bit <= 4);
        }

        // ── 일괄 제어 로직 ─────────────────────────────────────────

        private void ToggleAllForwardHigh()
        {
            bool target = !AllForwardHighOn; // 현재 상태 반대로
            ApplyGroupOutputs(
                it => it.IOName == "정방향" || it.IOName == "고속",
                target,
                "정방향 고속"
            );
            AllForwardHighOn = target;
        }

        private void ToggleAllReverseHigh()
        {
            bool target = !AllReverseHighOn;
            ApplyGroupOutputs(
                it => it.IOName == "역방향" || it.IOName == "고속",
                target,
                "역방향 고속"
            );
            AllReverseHighOn = target;
        }

        private void ToggleAllForwardLow()
        {
            bool target = !AllForwardLowOn;
            ApplyGroupOutputs(
                it => it.IOName == "정방향" || it.IOName == "저속",
                target,
                "정방향 저속"
            );
            AllForwardLowOn = target;
        }

        private void ToggleAllReverseLow()
        {
            bool target = !AllReverseLowOn;
            ApplyGroupOutputs(
                it => it.IOName == "역방향" || it.IOName == "저속",
                target,
                "역방향 저속"
            );
            AllReverseLowOn = target;
        }

        /// <summary>
        /// 조건에 맞는 I/O들을 한 번에 ON / OFF
        /// </summary>
        private void ApplyGroupOutputs(Func<IOMonitorItem, bool> predicate, bool turnOn, string caption)
        {
            if (!_hwConnected)
            {
                PostPopup(
                    "보드가 연결되지 않았습니다.\r\n(H/W: Disconnected 상태)",
                    "인버터 일괄 제어 - " + caption,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            var targets = IOList.Where(predicate).ToList();
            if (targets.Count == 0)
                return;

            foreach (var item in targets)
                ForceWriteOutput(item, turnOn);
        }

        /// <summary>
        /// 개별 I/O를 원하는 값(true/false)으로 강제 쓰기
        /// </summary>
        private void ForceWriteOutput(IOMonitorItem item, bool value)
        {
            if (item == null) return;

            ParsedAddr p;
            if (!_addrCache.TryGetValue(item, out p))
                return;

            if (!p.IsOutput)
                return;

            ushort bits;
            if (!MdFunc32Wrapper.TryReadBlock16(p.Station, p.DevType, p.BlockStart, out bits))
                return;

            ushort mask = (ushort)(1 << p.BitOffset);

            if (value)
                bits = (ushort)(bits | mask);
            else
                bits = (ushort)(bits & ~mask);

            if (MdFunc32Wrapper.TryWriteBlock16(p.Station, p.DevType, p.BlockStart, bits))
            {
                item.CurrentState = value;
            }
        }

        private void ShowHelp()
        {
            string msg =
"인버터 모니터 사용법\r\n\r\n" +
"• LBS_Conv 테이블의 ID(예: CONV1), InvStationNo, CnvDirection을 기준으로\r\n" +
"  각 컨베이어별 4개의 인버터 출력이 생성됩니다.\r\n" +
"   - bit0: 정방향 출력\r\n" +
"   - bit1: 역방향 출력\r\n" +
"   - bit3: 고속 주행\r\n" +
"   - bit4: 저속 주행\r\n\r\n" +
"• CONV 그룹 헤더에는 CnvDirection(12=IN, 6=OUT)과 InvStationNo가 함께 표기됩니다.\r\n" +
"   예) CONV1 (IN, ST#31)\r\n\r\n" +
"• 상단 일괄 제어 버튼으로 모든 CONV에 대해\r\n" +
"  정/역방향 고속, 정/역방향 저속을 한 번에 ON/OFF 할 수 있습니다.\r\n" +
"  버튼 텍스트의 ON/OFF는 마지막으로 실행한 상태 기준입니다.\r\n" +
"• 각 행의 OUTPUT 버튼은 해당 I/O만 개별적으로 토글합니다.\r\n" +
"• 체크/메모 기능은 IO 모니터와 동일하게 동작합니다.\r\n\r\n" +
"저장 파일: " + StateFilePath;

            PostPopup(msg, "도움말 — Inverter Monitor", System.Windows.MessageBoxImage.Information);
        }
    }
}
