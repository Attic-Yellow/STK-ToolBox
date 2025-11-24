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
    public class IOCheckViewModel : IoMonitorViewModelBase
    {
        public ObservableCollection<TabPageVM> Tabs { get; private set; }

        private TabPageVM _selectedTab;
        public TabPageVM SelectedTab
        {
            get { return _selectedTab; }
            set
            {
                if (_selectedTab == value) return;
                _selectedTab = value;
                OnPropertyChanged("SelectedTab");
                var _ = PollVisibleItemsAsync();
            }
        }

        public ICommand RefreshCommand { get; private set; }
        public ICommand HelpCommand { get; private set; }

        // ★ 추가된 커맨드 4개
        public ICommand CheckAllCommand { get; private set; }
        public ICommand UncheckAllCommand { get; private set; }
        public ICommand CheckCurrentTabCommand { get; private set; }
        public ICommand UncheckCurrentTabCommand { get; private set; }

        private readonly string _dbPath = @"D:\LBS_DB\LBSControl.db3";

        private const short ChannelNo = 81;

        public IOCheckViewModel()
            : base(ChannelNo, "io_check_state.csv")
        {
            Tabs = new ObservableCollection<TabPageVM>();

            RefreshCommand = new RelayCommand(new Action(LoadIOStatus));
            HelpCommand = new RelayCommand(new Action(ShowHelp));

            // ★ 커맨드 초기화
            CheckAllCommand = new RelayCommand(new Action(CheckAllNotSpare));
            UncheckAllCommand = new RelayCommand(new Action(UncheckAllNotSpare));
            CheckCurrentTabCommand = new RelayCommand(new Action(CheckCurrentTabNotSpare));
            UncheckCurrentTabCommand = new RelayCommand(new Action(UncheckCurrentTabNotSpare));

            // IOByteTable_X / Y 로드
            MdFunc32Wrapper.LoadIoByteTables(_dbPath);

            LoadIOStatus();
        }

        private void LoadIOStatus()
        {
            IOList.Clear();

            try
            {
                if (!File.Exists(_dbPath))
                {
                    PostPopup("SQLite DB 파일을 찾을 수 없습니다:\r\n" + _dbPath,
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

                BuildAddressCache();
                LoadSavedStates(true);
                RebuildTabsStable();
                var _ = PollVisibleItemsAsync();
            }
            catch (Exception ex)
            {
                PostPopup("로드 오류: " + ex.Message,
                          "오류",
                          System.Windows.MessageBoxImage.Error);
            }
        }

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
                }
            }
            return -1;
        }

        private static string Read(SQLiteDataReader reader, int ord)
        {
            if (ord < 0 || reader.IsDBNull(ord))
                return "";
            return reader.GetValue(ord) + "";
        }

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
                newTabs.Add(new TabPageVM("EMPTY|0", "N/A", new List<IOMonitorItem>()));

            Tabs.Clear();
            foreach (var t in newTabs) Tabs.Add(t);

            SelectedTab = Tabs.FirstOrDefault(t => t.Key == oldKey) ?? Tabs.FirstOrDefault();
        }

        private void ShowHelp()
        {
            string msg =
"I/O 모니터 사용법\r\n\r\n" +
"• 저장/불러오기: Checked 상태와 메모(Note)을 CSV로 저장/적용.\r\n" +
"• 탭: DetailUnit 순서 그대로, 한 탭에 32개(좌16/우16) 표시.\r\n" +
"• Output: Y 주소(출력)만 토글 가능.\r\n" +
"• Checked: 체크 시 행 배경이 강조됩니다.\r\n" +
"• 메모: 각 행의 '메모' 버튼으로 메모 작성/수정/삭제.\r\n\r\n" +
"저장 파일: " + StateFilePath;

            PostPopup(msg, "도움말 — I/O Monitor", System.Windows.MessageBoxImage.Information);
        }

        protected override IEnumerable<IOMonitorItem> GetVisibleItems()
        {
            if (SelectedTab == null)
                return new IOMonitorItem[0];

            return SelectedTab.Left16.Concat(SelectedTab.Right16);
        }

        // ─────────────────────────────────────
        //   Spare 제외 체크/해제 로직
        //   기준: IOName 안에 "Spare" (대소문자 무시)
        // ─────────────────────────────────────

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

        private void SetCheckForItems(IEnumerable<IOMonitorItem> items, bool check)
        {
            if (items == null) return;

            foreach (var it in items)
            {
                if (it == null) continue;
                if (IsSpare(it)) continue;          // Spare 제외
                it.IsChecked = check;
            }
        }
    }

    public class TabPageVM
    {
        public string Key { get; private set; }
        public string Header { get; private set; }
        public List<IOMonitorItem> Items { get; private set; }

        public TabPageVM(string key, string header, List<IOMonitorItem> items)
        {
            Key = key;
            Header = header;
            Items = items ?? new List<IOMonitorItem>();
        }

        public IEnumerable<IOMonitorItem> Left16
        {
            get { return Items.Take(16); }
        }

        public IEnumerable<IOMonitorItem> Right16
        {
            get { return Items.Skip(16).Take(16); }
        }
    }
}
