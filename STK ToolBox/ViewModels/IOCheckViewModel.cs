using STK_ToolBox.Helpers;
using STK_ToolBox.Models;
using STK_ToolBox.ViewModels.BaseSource;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Windows.Input;

namespace STK_ToolBox.ViewModels
{
    /// <summary>
    /// 공용 I/O 모니터 화면 ViewModel.
    /// - IOMonitoring 테이블을 읽어 IOList 구성
    /// - DetailUnit 기준으로 탭 분할 (한 탭당 32개: 좌16 / 우16)
    /// - 체크/메모/자동 저장은 IoMonitorViewModelBase 공통 로직 사용
    /// - Spare 포함 항목을 제외하고 전체/현재 탭 Check On/Off 기능 제공
    /// </summary>
    public class IOCheckViewModel : IoMonitorViewModelBase
    {
        #region Constants & Fields

        private readonly string _dbPath = @"D:\LBS_DB\LBSControl.db3";

        private const short ChannelNo = 81;

        private TabPageVM _selectedTab;

        #endregion

        #region Properties

        /// <summary>
        /// DetailUnit 기준으로 구성된 탭 컬렉션.
        /// 한 탭에는 최대 32개(I/O)가 들어가며, UI에서는 좌16/우16으로 나눠 표시.
        /// </summary>
        public ObservableCollection<TabPageVM> Tabs { get; private set; }

        /// <summary>
        /// 현재 선택된 탭.
        /// 변경 시 해당 탭의 보이는 항목을 기준으로 Polling 수행.
        /// </summary>
        public TabPageVM SelectedTab
        {
            get { return _selectedTab; }
            set
            {
                if (_selectedTab == value) return;
                _selectedTab = value;
                OnPropertyChanged("SelectedTab");

                // 탭 변경 시 현재 보이는 항목만 즉시 Poll
                var _ = PollVisibleItemsAsync();
            }
        }

        #endregion

        #region Commands

        public ICommand RefreshCommand { get; private set; }
        public ICommand HelpCommand { get; private set; }

        /// <summary>전체 IO Check (Spare 제외)</summary>
        public ICommand CheckAllCommand { get; private set; }

        /// <summary>전체 IO Uncheck (Spare 제외)</summary>
        public ICommand UncheckAllCommand { get; private set; }

        /// <summary>현재 탭 IO Check (Spare 제외)</summary>
        public ICommand CheckCurrentTabCommand { get; private set; }

        /// <summary>현재 탭 IO Uncheck (Spare 제외)</summary>
        public ICommand UncheckCurrentTabCommand { get; private set; }

        #endregion

        #region Constructor

        public IOCheckViewModel()
            : base(ChannelNo, "io_check_state.csv")
        {
            Tabs = new ObservableCollection<TabPageVM>();

            RefreshCommand = new RelayCommand(new Action(LoadIOStatus));
            HelpCommand = new RelayCommand(new Action(ShowHelp));

            // Spare 제외 Check 관련 커맨드
            CheckAllCommand = new RelayCommand(new Action(CheckAllNotSpare));
            UncheckAllCommand = new RelayCommand(new Action(UncheckAllNotSpare));
            CheckCurrentTabCommand = new RelayCommand(new Action(CheckCurrentTabNotSpare));
            UncheckCurrentTabCommand = new RelayCommand(new Action(UncheckCurrentTabNotSpare));

            // IOByteTable_X / Y 로드 (CC-Link 주소 범위 체크용)
            MdFunc32Wrapper.LoadIoByteTables(_dbPath);

            LoadIOStatus();
        }

        #endregion

        #region DB Load & IOList 구성

        /// <summary>
        /// IOMonitoring 테이블에서 I/O 정보를 읽어 IOList를 구성.
        /// 이후 주소 캐시 재구성, 저장된 상태 복원, 탭 재구성까지 수행.
        /// </summary>
        private void LoadIOStatus()
        {
            IOList.Clear();

            try
            {
                if (!File.Exists(_dbPath))
                {
                    PostPopup(
                        "SQLite DB 파일을 찾을 수 없습니다:\r\n" + _dbPath,
                        "DB 연결 오류",
                        System.Windows.MessageBoxImage.Error);
                    return;
                }

                using (var conn = new SQLiteConnection("Data Source=" + _dbPath + ";Version=3;"))
                {
                    conn.Open();

                    using (var cmd = new SQLiteCommand("SELECT * FROM IOMonitoring;", conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int idIdx = Find(reader, "ID", "Id", "io_id", "rowid");
                            int nameIdx = Find(reader, "IOName", "Name", "IoName");
                            int addrIdx = Find(reader, "Address", "IOAddress", "Addr");
                            int unitIdx = Find(reader, "Unit", "unit");
                            int detIdx = Find(reader, "DetailUnit", "detailunit", "detailUnit");
                            int dscIdx = Find(reader, "Description", "Desc", "Remark", "Remarks");

                            var item = new IOMonitorItem
                            {
                                Id = (idIdx >= 0 && !reader.IsDBNull(idIdx))
                                    ? Convert.ToInt32(reader.GetValue(idIdx))
                                    : 0,
                                IOName = Read(reader, nameIdx),
                                Address = Read(reader, addrIdx),
                                Unit = Read(reader, unitIdx),
                                DetailUnit = Read(reader, detIdx),
                                Description = Read(reader, dscIdx),
                                CurrentState = false
                            };

                            IOList.Add(item);
                        }
                    }
                }

                // 주소 캐시 재작성 + 저장 상태 복원 + 탭 구성 + Poll
                BuildAddressCache();
                LoadSavedStates(true);
                RebuildTabsStable();
                var _ = PollVisibleItemsAsync();
            }
            catch (Exception ex)
            {
                PostPopup(
                    "로드 오류: " + ex.Message,
                    "오류",
                    System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 여러 컬럼 후보명 중 첫 번째로 존재하는 인덱스를 반환.
        /// </summary>
        private static int Find(SQLiteDataReader reader, string a, params string[] others)
        {
            var names = new List<string>();
            names.Add(a);
            if (others != null) names.AddRange(others);

            foreach (string name in names)
            {
                try
                {
                    int idx = reader.GetOrdinal(name);
                    if (idx >= 0) return idx;
                }
                catch
                {
                    // ignore
                }
            }
            return -1;
        }

        /// <summary>
        /// 지정 인덱스에서 문자열을 안전하게 읽어옴(없거나 NULL이면 빈 문자열).
        /// </summary>
        private static string Read(SQLiteDataReader reader, int ord)
        {
            if (ord < 0 || reader.IsDBNull(ord))
                return string.Empty;

            return reader.GetValue(ord) + string.Empty;
        }

        #endregion

        #region 탭 구성 로직 (DetailUnit 그룹 + 32개씩 분할)

        /// <summary>
        /// DetailUnit 기준으로 그룹핑 후,
        /// 한 그룹(DetailUnit) 내에서 32개씩 끊어서 탭(TabPageVM)을 다시 구성.
        /// 기존 SelectedTab의 Key를 기준으로 선택 탭 유지 시도.
        /// </summary>
        private void RebuildTabsStable()
        {
            string oldKey = (SelectedTab != null) ? SelectedTab.Key : null;

            var groupOrder = new List<string>();
            var bucket = new Dictionary<string, List<IOMonitorItem>>();

            foreach (var it in IOList)
            {
                string detail = string.IsNullOrWhiteSpace(it.DetailUnit) ? "N/A" : it.DetailUnit;
                if (!bucket.ContainsKey(detail))
                {
                    bucket[detail] = new List<IOMonitorItem>();
                    groupOrder.Add(detail);
                }
                bucket[detail].Add(it);
            }

            const int pageSize = 32;
            var newTabs = new List<TabPageVM>();

            foreach (string detail in groupOrder)
            {
                List<IOMonitorItem> items = bucket[detail];

                for (int page = 0; page < items.Count; page += pageSize)
                {
                    var chunk = items.Skip(page).Take(pageSize).ToList();
                    string key = detail + "|" + (page / pageSize);
                    string title = detail;

                    newTabs.Add(new TabPageVM(key, title, chunk));
                }
            }

            if (newTabs.Count == 0)
            {
                newTabs.Add(new TabPageVM("EMPTY|0", "N/A", new List<IOMonitorItem>()));
            }

            Tabs.Clear();
            foreach (var t in newTabs)
                Tabs.Add(t);

            SelectedTab = Tabs.FirstOrDefault(t => t.Key == oldKey) ?? Tabs.FirstOrDefault();
        }

        #endregion

        #region Help & Override

        private void ShowHelp()
        {
            string msg =
"I/O 모니터 사용법\r\n\r\n" +
"• 저장/불러오기: Checked 상태와 메모(Note)를 CSV로 저장/적용합니다.\r\n" +
"• 탭: DetailUnit 순서 그대로, 한 탭에 최대 32개(좌16/우16)씩 표시됩니다.\r\n" +
"• Output: Y 주소(출력)만 토글 가능하며, CC-Link 보드와 연결되어야 합니다.\r\n" +
"• Checked: 체크 시 행 배경이 강조됩니다.\r\n" +
"• 메모: 각 행의 '메모' 버튼으로 특이사항을 기록/수정/삭제할 수 있습니다.\r\n\r\n" +
"저장 파일 경로: " + StateFilePath;

            PostPopup(
                msg,
                "도움말 — I/O Monitor",
                System.Windows.MessageBoxImage.Information);
        }

        /// <summary>
        /// 현재 선택된 탭에 표시되는 항목들(좌16 + 우16)을 Polling 대상으로 반환.
        /// </summary>
        protected override IEnumerable<IOMonitorItem> GetVisibleItems()
        {
            if (SelectedTab == null)
                return new IOMonitorItem[0];

            return SelectedTab.Left16.Concat(SelectedTab.Right16);
        }

        #endregion

        #region Spare 제외 체크/해제 유틸

        /// <summary>
        /// IOName 에 "Spare" (대소문자 무시) 가 포함되어 있으면 Spare로 간주.
        /// </summary>
        private static bool IsSpare(IOMonitorItem item)
        {
            if (item == null) return false;

            string name = item.IOName;
            if (string.IsNullOrEmpty(name)) return false;

            return name.IndexOf("spare", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void CheckAllNotSpare()
        {
            SetCheckForItems(IOList, true);
        }

        private void UncheckAllNotSpare()
        {
            SetCheckForItems(IOList, false);
        }

        private void CheckCurrentTabNotSpare()
        {
            if (SelectedTab == null) return;
            SetCheckForItems(SelectedTab.Items, true);
        }

        private void UncheckCurrentTabNotSpare()
        {
            if (SelectedTab == null) return;
            SetCheckForItems(SelectedTab.Items, false);
        }

        /// <summary>
        /// 전달된 I/O 리스트에서 Spare가 아닌 항목에 대해 IsChecked 값을 일괄 변경.
        /// </summary>
        private void SetCheckForItems(IEnumerable<IOMonitorItem> items, bool check)
        {
            if (items == null) return;

            foreach (var it in items)
            {
                if (it == null) continue;
                if (IsSpare(it)) continue;     // Spare 제외
                it.IsChecked = check;
            }
        }

        #endregion
    }

    #region TabPageVM

    /// <summary>
    /// I/O 모니터 탭 한 개를 표현하는 ViewModel.
    /// - Key : 내부 식별(DetailUnit + 페이지 인덱스)
    /// - Header : 탭에 표시되는 제목(DetailUnit)
    /// - Items  : 해당 탭에 포함된 I/O 항목 리스트 (최대 32개)
    ///   Left16 / Right16 으로 16개씩 나누어 UI에서 좌/우 그리드에 바인딩.
    /// </summary>
    public class TabPageVM
    {
        #region Properties

        public string Key { get; private set; }
        public string Header { get; private set; }
        public List<IOMonitorItem> Items { get; private set; }

        /// <summary>첫 16개 항목(또는 전체가 16개 미만이면 그만큼) – 좌측 그리드.</summary>
        public IEnumerable<IOMonitorItem> Left16
        {
            get { return Items.Take(16); }
        }

        /// <summary>17번째 이후 최대 16개 항목 – 우측 그리드.</summary>
        public IEnumerable<IOMonitorItem> Right16
        {
            get { return Items.Skip(16).Take(16); }
        }

        #endregion

        #region Constructor

        public TabPageVM(string key, string header, List<IOMonitorItem> items)
        {
            Key = key;
            Header = header;
            Items = items ?? new List<IOMonitorItem>();
        }

        #endregion
    }

    #endregion
}
