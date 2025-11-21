using STK_ToolBox.Helpers;
using STK_ToolBox.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Threading.Tasks;

namespace STK_ToolBox.ViewModels
{
    public class IOCheckViewModel : BaseViewModel
    {
        public ObservableCollection<IOMonitorItem> IOList { get; } = new ObservableCollection<IOMonitorItem>();
        public ObservableCollection<TabPageVM> Tabs { get; } = new ObservableCollection<TabPageVM>();

        private TabPageVM _selectedTab;
        public TabPageVM SelectedTab
        {
            get { return _selectedTab; }
            set
            {
                if (_selectedTab == value) return;
                _selectedTab = value;
                OnPropertyChanged();
                var _ = PollVisibleItemsAsync();
            }
        }

        private bool _hwConnected;
        public string HwStatusText { get { return _hwConnected ? "H/W: Connected" : "H/W: Disconnected"; } }
        public Brush HwStatusBrush { get { return _hwConnected ? Brushes.SeaGreen : Brushes.IndianRed; } }

        public string StateFilePath { get { return _stateFile; } }

        public ICommand RefreshCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand LoadStateCommand { get; }
        public ICommand HelpCommand { get; }
        public ICommand ToggleOutputCommand { get; }
        public ICommand OpenNoteCommand { get; }

        private readonly string DbPath = @"D:\LBS_DB\LBSControl.db3";
        private readonly short _channelNo = 81;

        private readonly string _stateDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "STK_ToolBox");
        private readonly string _stateFile;

        private readonly Dictionary<string, string> _notes =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // 주소 파싱 캐시
        private class ParsedAddr
        {
            public short DevType;     // MdFunc32Wrapper.DevX / DevY
            public short Station;     // 1..n
            public int Bit;           // 0..31
            public short BlockStart;  // 0 또는 16
            public int BitOffset;     // 0..15
            public bool IsOutput { get { return DevType == MdFunc32Wrapper.DevY; } }
        }
        private readonly Dictionary<IOMonitorItem, ParsedAddr> _addrCache =
            new Dictionary<IOMonitorItem, ParsedAddr>();

        private readonly DispatcherTimer _pollTimer;
        private bool _polling;

        public IOCheckViewModel()
        {
            _stateFile = Path.Combine(_stateDir, "io_check_state.csv");
            MdFunc32Wrapper.LoadIoByteTables(DbPath);

            RefreshCommand = new RelayCommand(() => LoadIOStatus());
            SaveCommand = new RelayCommand(() => SaveStates());
            LoadStateCommand = new RelayCommand(() => LoadSavedStates());
            HelpCommand = new RelayCommand(() => ShowHelp());
            ToggleOutputCommand = new RelayCommand<IOMonitorItem>(ToggleOutput, it => it != null && it.CanToggle);
            OpenNoteCommand = new RelayCommand<IOMonitorItem>(OpenNote);

            LoadIOStatus();

            _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _pollTimer.Tick += async (s, e) => await PollVisibleItemsAsync();
            _pollTimer.Start();
        }


        public void OnViewLoaded()
        {
            var rc = MdFunc32Wrapper.Open(_channelNo);
            _hwConnected = (rc == 0);
            OnPropertyChanged(nameof(HwStatusText));
            OnPropertyChanged(nameof(HwStatusBrush));

            if (!_hwConnected)
                PostPopup("로드 오류: mdOpen 실패(chan=" + _channelNo + ", rc=" + rc + ")", "오류", MessageBoxImage.Error);

            var _ = PollVisibleItemsAsync();
        }

        private void PostPopup(string text, string title, MessageBoxImage icon)
        {
            var d = Application.Current != null ? Application.Current.Dispatcher : null;
            if (d == null) return;
            d.BeginInvoke(new Action(() => MessageBox.Show(text, title, MessageBoxButton.OK, icon)),
                          DispatcherPriority.Background);
        }

        private void OpenNote(IOMonitorItem item)
        {
            if (item == null) return;
            var key = GetKey(item);
            string current;
            _notes.TryGetValue(key, out current);

            var result = NotePrompt.Show("특이사항 코멘트 입력창",
                "해당 I/O 항목에 대한 메모를 입력하세요.\r\n빈 칸으로 저장하면 메모가 삭제됩니다.",
                current ?? string.Empty);
            if (result == null) return;

            var text = result.Trim();
            if (string.IsNullOrEmpty(text)) _notes.Remove(key);
            else _notes[key] = text;
        }

        private void ToggleOutput(IOMonitorItem item)
        {
            if (item == null)
            {
                PostPopup("선택된 I/O 항목이 없습니다.", "I/O 출력", MessageBoxImage.Warning);
                return;
            }

            if (!item.CanToggle)
            {
                PostPopup("이 항목은 출력(Y)가 아니거나 토글 불가로 설정되어 있습니다.\r\nAddress=" + item.Address,
                          "I/O 출력", MessageBoxImage.Warning);
                return;
            }

            if (!_hwConnected)
            {
                PostPopup("보드가 연결되지 않았습니다.\r\n(H/W: Disconnected 상태)", "I/O 출력", MessageBoxImage.Warning);
                return;
            }

            ParsedAddr p;
            if (!_addrCache.TryGetValue(item, out p))
            {
                PostPopup("주소 파싱 실패: " + item.Address,
                          "I/O 출력", MessageBoxImage.Warning);
                return;
            }

            if (!p.IsOutput)
            {
                PostPopup("출력(Y) 주소가 아닙니다: " + item.Address,
                          "I/O 출력", MessageBoxImage.Warning);
                return;
            }


            ushort bits;
            if (!MdFunc32Wrapper.TryReadBlock16(p.Station, p.DevType, p.BlockStart, out bits))
            {
                PostPopup("블록 읽기 실패: " + item.Address,
                          "I/O 출력", MessageBoxImage.Warning);
                return;
            }

            ushort mask = (ushort)(1 << p.BitOffset);
            bool newValue = !item.CurrentState;
            bits = newValue ? (ushort)(bits | mask) : (ushort)(bits & ~mask);

            if (MdFunc32Wrapper.TryWriteBlock16(p.Station, p.DevType, p.BlockStart, bits))
            {
                item.CurrentState = newValue;
            }
            else
            {
                PostPopup("출력 토글 실패(Write 실패): " + item.Address,
                          "I/O 출력", MessageBoxImage.Warning);
            }
        }


        private void LoadIOStatus()
        {
            IOList.Clear();
            try
            {
                if (!File.Exists(DbPath))
                {
                    PostPopup("SQLite DB 파일을 찾을 수 없습니다:\r\n" + DbPath, "DB 연결 오류", MessageBoxImage.Error);
                    return;
                }

                using (var conn = new SQLiteConnection("Data Source=" + DbPath + ";Version=3;"))
                {
                    conn.Open();
                    using (var cmd = new SQLiteCommand("SELECT * FROM IOMonitoring;", conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int Find(string a, params string[] b)
                            {
                                foreach (var name in new[] { a }.Concat(b))
                                {
                                    try { var i = reader.GetOrdinal(name); if (i >= 0) return i; } catch { }
                                }
                                return -1;
                            }
                            Func<int, string> Read = ord => (ord < 0 || reader.IsDBNull(ord)) ? "" : (reader.GetValue(ord) + "");

                            int idIdx = Find("ID", "Id", "io_id", "rowid");
                            int nameIdx = Find("IOName", "Name", "IoName");
                            int addrIdx = Find("Address", "IOAddress", "Addr");
                            int unitIdx = Find("Unit", "unit");
                            int detIdx = Find("DetailUnit", "detailunit", "detailUnit");
                            int dscIdx = Find("Description", "Desc", "Remark", "Remarks");

                            var item = new IOMonitorItem
                            {
                                Id = (idIdx >= 0 && !reader.IsDBNull(idIdx)) ? Convert.ToInt32(reader.GetValue(idIdx)) : 0,
                                IOName = Read(nameIdx),
                                Address = Read(addrIdx),
                                Unit = Read(unitIdx),
                                DetailUnit = Read(detIdx),
                                Description = Read(dscIdx),
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
                PostPopup("로드 오류: " + ex.Message, "오류", MessageBoxImage.Error);
            }
        }

        private void SaveStates()
        {
            try
            {
                if (!Directory.Exists(_stateDir)) Directory.CreateDirectory(_stateDir);

                var sb = new StringBuilder();
                sb.AppendLine("Id,Address,IOName,IsChecked,Note");
                foreach (var it in IOList)
                {
                    int idPart = it.Id;
                    string addrPart = (it.Address ?? "").Replace(",", "");
                    string namePart = (it.IOName ?? "").Replace(",", "");
                    bool isChecked = it.IsChecked;
                    string note;
                    _notes.TryGetValue(GetKey(it), out note);
                    note = (note ?? "").Replace("\r", " ").Replace("\n", " ").Replace(",", "，");
                    sb.AppendLine(idPart + "," + addrPart + "," + namePart + "," + isChecked + "," + note);
                }

                File.WriteAllText(_stateFile, sb.ToString(), Encoding.UTF8);
                PostPopup("저장 완료\r\n" + _stateFile, "저장", MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                PostPopup("저장 중 오류: " + ex.Message, "저장 오류", MessageBoxImage.Error);
            }
        }

        private void LoadSavedStates(bool silent = false)
        {
            try
            {
                if (!File.Exists(_stateFile))
                {
                    if (!silent) PostPopup("저장된 체크 상태 파일이 없습니다.", "불러오기", MessageBoxImage.Information);
                    return;
                }

                var lines = File.ReadAllLines(_stateFile, Encoding.UTF8);
                int appliedCheck = 0, appliedNote = 0;

                foreach (var line in lines.Skip(1))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var raw = line.Split(',');
                    if (raw.Length < 4) continue;

                    int id; int.TryParse(raw[0], out id);
                    string addr = (raw[1] ?? "").Trim();
                    string name = (raw[2] ?? "").Trim();
                    bool isChecked; bool.TryParse(raw[3], out isChecked);

                    string note = raw.Length >= 5 ? string.Join(",", raw.Skip(4)).Trim() : null;

                    IOMonitorItem target = null;
                    if (id != 0) target = IOList.FirstOrDefault(x => x.Id == id);
                    if (target == null && (!string.IsNullOrEmpty(addr) || !string.IsNullOrEmpty(name)))
                        target = IOList.FirstOrDefault(x =>
                            string.Equals(x.Address ?? "", addr, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(x.IOName ?? "", name, StringComparison.OrdinalIgnoreCase));

                    if (target != null)
                    {
                        target.IsChecked = isChecked;
                        appliedCheck++;
                        var key = GetKey(target);
                        if (!string.IsNullOrEmpty(note)) { _notes[key] = note; appliedNote++; }
                    }
                }

                if (!silent)
                    PostPopup("불러오기 완료: 체크 " + appliedCheck + "개, 메모 " + appliedNote + "개 적용\r\n" + _stateFile,
                              "불러오기", MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                if (!silent) PostPopup("불러오는 중 오류: " + ex.Message, "불러오기 오류", MessageBoxImage.Error);
            }
        }

        private void RebuildTabsStable()
        {
            var oldKey = SelectedTab != null ? SelectedTab.Key : null;

            var groupOrder = new List<string>();
            var bucket = new Dictionary<string, List<IOMonitorItem>>();

            foreach (var it in IOList)
            {
                var detail = string.IsNullOrWhiteSpace(it.DetailUnit) ? "N/A" : it.DetailUnit;
                if (!bucket.ContainsKey(detail))
                {
                    bucket[detail] = new List<IOMonitorItem>();
                    groupOrder.Add(detail);
                }
                bucket[detail].Add(it);
            }

            const int pageSize = 32;
            var newTabs = new List<TabPageVM>();

            foreach (var detail in groupOrder)
            {
                var items = bucket[detail];
                for (int page = 0; page < items.Count; page += pageSize)
                {
                    var chunk = items.Skip(page).Take(pageSize).ToList();
                    var key = detail + "|" + (page / pageSize);
                    var title = detail;
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
            var msg =
"I/O 모니터 사용법\r\n\r\n" +
"• 저장/불러오기: Checked 상태와 메모(Note)을 CSV로 저장/적용.\r\n" +
"• 탭: DetailUnit 순서 그대로, 한 탭에 32개(좌16/우16) 표시.\r\n" +
"• Output: Y 주소(출력)만 토글 가능.\r\n" +
"• Checked: 체크 시 행 배경이 강조됩니다.\r\n" +
"• 메모: 각 행의 '메모' 버튼으로 메모 작성/수정/삭제.\r\n\r\n" +
"저장 파일: " + StateFilePath;
            PostPopup(msg, "도움말 — I/O Monitor", MessageBoxImage.Information);
        }

        private void BuildAddressCache()
        {
            _addrCache.Clear();
            foreach (var it in IOList)
            {
                if (string.IsNullOrWhiteSpace(it.Address)) continue;
                short devType; short st; int bit;
                if (MdFunc32Wrapper.TryParseForBlock(it.Address.Trim(), out devType, out st, out bit))
                {
                    short block = (short)((bit / 16) * 16);
                    _addrCache[it] = new ParsedAddr
                    {
                        DevType = devType,
                        Station = st,
                        Bit = bit,
                        BlockStart = block,
                        BitOffset = bit % 16
                    };
                }
            }
        }

        private async Task PollVisibleItemsAsync()
        {
            if (SelectedTab == null || !_hwConnected) return;
            if (_polling) return;
            _polling = true;

            try
            {
                var visible = SelectedTab.Left16.Concat(SelectedTab.Right16)
                                .Where(it => it != null && _addrCache.ContainsKey(it))
                                .ToList();
                if (visible.Count == 0) return;

                // 문자열 키 (DevType:Station:BlockStart)
                var groups = visible.GroupBy(it =>
                {
                    var p = _addrCache[it];
                    return p.DevType + ":" + p.Station + ":" + p.BlockStart;
                }).ToList();

                var results = new Dictionary<IOMonitorItem, bool>();
                await Task.Run(() =>
                {
                    foreach (var g in groups)
                    {
                        string k = g.Key;
                        var first = _addrCache[g.First()];
                        ushort bits;
                        if (!MdFunc32Wrapper.TryReadBlock16(first.Station, first.DevType, first.BlockStart, out bits))
                            continue;

                        foreach (var it in g)
                        {
                            var p = _addrCache[it];
                            bool on = ((bits >> p.BitOffset) & 1) != 0;
                            results[it] = on;
                        }
                    }
                });

                foreach (var kv in results)
                    kv.Key.CurrentState = kv.Value;
            }
            catch
            {
                // swallow
            }
            finally
            {
                _polling = false;
            }
        }

        private static string GetKey(IOMonitorItem it)
        {
            if (it == null) return "";
            if (it.Id != 0) return "ID:" + it.Id;
            var addr = it.Address ?? "";
            var name = it.IOName ?? "";
            return "AK:" + addr + "|" + name;
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

        public IEnumerable<IOMonitorItem> Left16 { get { return Items.Take(16); } }
        public IEnumerable<IOMonitorItem> Right16 { get { return Items.Skip(16).Take(16); } }
    }
}