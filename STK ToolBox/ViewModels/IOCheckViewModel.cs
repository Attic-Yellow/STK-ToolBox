using Microsoft.VisualBasic;
using Microsoft.Xaml.Behaviors;
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

namespace STK_ToolBox.ViewModels
{
    public class IOCheckViewModel : BaseViewModel
    {
        // Public Bindings
        public ObservableCollection<IOMonitorItem> IOList { get; } = new ObservableCollection<IOMonitorItem>();
        public ObservableCollection<TabPageVM> Tabs { get; } = new ObservableCollection<TabPageVM>();

        private TabPageVM _selectedTab;
        public TabPageVM SelectedTab
        {
            get => _selectedTab;
            set
            {
                if (_selectedTab == value) return;
                _selectedTab = value;
                OnPropertyChanged();
                // 탭 바꾸면 보이는 항목 즉시 갱신
                PollVisibleItems();
            }
        }

        // Status bar (H/W 연결 표시)
        private bool _hwConnected;
        public string HwStatusText => _hwConnected ? "H/W: Connected" : "H/W: Disconnected";
        public Brush HwStatusBrush => _hwConnected ? Brushes.SeaGreen : Brushes.IndianRed;

        // 상태 파일 경로(UI 표시)
        public string StateFilePath => _stateFile;

        // Commands
        public ICommand RefreshCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand LoadStateCommand { get; }
        public ICommand HelpCommand { get; }
        public ICommand ToggleOutputCommand { get; }
        public ICommand OpenNoteCommand { get; }

        // Config / Paths
        private readonly string DbPath = @"D:\LBS_DB\LBSControl.db3";
        private readonly int _stationNo = 0; // CC-Link Station 번호

        private readonly string _stateDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "STK_ToolBox");
        private readonly string _stateFile;

        // 메모 저장소 (키: See GetKey)
        private readonly Dictionary<string, string> _notes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // 폴링 타이머
        private readonly DispatcherTimer _pollTimer;

        public IOCheckViewModel()
        {
            _stateFile = Path.Combine(_stateDir, "io_check_state.csv");

            RefreshCommand = new RelayCommand(() => LoadIOStatus());
            SaveCommand = new RelayCommand(() => SaveStates());
            LoadStateCommand = new RelayCommand(() => LoadSavedStates());
            HelpCommand = new RelayCommand(() => ShowHelp());
            ToggleOutputCommand = new RelayCommand<IOMonitorItem>(ToggleOutput, it => it?.CanToggle == true);
            OpenNoteCommand = new RelayCommand<IOMonitorItem>(OpenNote);

            // 데이터 로드 → 보드 연결 → 폴링 시작
            LoadIOStatus();
            TryOpenBoard();

            _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _pollTimer.Tick += (s, e) => PollVisibleItems();
            _pollTimer.Start();
        }

        // 메모 입력/수정
        private void OpenNote(IOMonitorItem item)
        {
            if (item == null) return;
            var key = GetKey(item);
            _notes.TryGetValue(key, out var current);

            var result = NotePrompt.Show("특이사항 코멘트 입력창",
                                         "해당 I/O 항목에 대한 메모를 입력하세요.\r\n빈 칸으로 저장하면 메모가 삭제됩니다.",
                                         current ?? string.Empty);
            if (result == null) return;           // 취소
            var text = result.Trim();

            if (string.IsNullOrEmpty(text))
                _notes.Remove(key);
            else
                _notes[key] = text;
        }

        // CC-Link 토글
        private void ToggleOutput(IOMonitorItem item)
        {
            if (item == null || !item.CanToggle) return;

            var ok = WriteBitSafe(item.Address, !item.CurrentState);
            if (!ok)
            {
                MessageBox.Show($"출력 토글 실패: {item.Address}\n{HwStatusText}",
                    "I/O 출력", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 성공 시 즉시 재독 → UI 반영
            if (ReadBitSafe(item.Address, out var on))
                item.CurrentState = on;
            else
                MessageBox.Show($"토글 후 읽기 실패: {item.Address}\n{HwStatusText}",
                    "I/O 출력", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        // DB 로드 + 상태 반영
        private void LoadIOStatus()
        {
            IOList.Clear();

            try
            {
                if (!File.Exists(DbPath))
                {
                    MessageBox.Show($"SQLite DB 파일을 찾을 수 없습니다:\n{DbPath}",
                        "DB 연결 오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                using (var conn = new SQLiteConnection($"Data Source={DbPath};Version=3;"))
                {
                    conn.Open();

                    using (var cmd = new SQLiteCommand("SELECT * FROM IOMonitoring;", conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        // DB 원래 순서대로 쌓음
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
                            string Read(int ord) => (ord < 0 || reader.IsDBNull(ord)) ? "" : (reader.GetValue(ord)?.ToString() ?? "");

                            int idIdx = Find("ID", "Id", "io_id", "rowid");
                            int nameIdx = Find("IOName", "Name", "IoName");
                            int addrIdx = Find("Address", "IOAddress", "Addr");
                            int unitIdx = Find("Unit", "unit");
                            int detIdx = Find("DetailUnit", "detailunit", "detailUnit");
                            int dscIdx = Find("Description", "Desc", "Remark", "Remarks");

                            IOList.Add(new IOMonitorItem
                            {
                                Id = (idIdx >= 0 && !reader.IsDBNull(idIdx)) ? Convert.ToInt32(reader.GetValue(idIdx)) : 0,
                                IOName = Read(nameIdx),
                                Address = Read(addrIdx),
                                Unit = Read(unitIdx),
                                DetailUnit = Read(detIdx),
                                Description = Read(dscIdx),
                                CurrentState = false // 폴링이 바로 갱신
                            });
                        }
                    }
                }

                // 체크/메모 상태 복원
                LoadSavedStates(silent: true);

                // 탭 재구성(DetailUnit 순서 유지, 32개/탭)
                RebuildTabsStable();

                // 첫 화면 즉시 폴링
                PollVisibleItems();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"로드 오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 저장/불러오기(CSV: Id,Address,IOName,IsChecked,Note)
        private void SaveStates()
        {
            try
            {
                if (!Directory.Exists(_stateDir))
                    Directory.CreateDirectory(_stateDir);

                var sb = new StringBuilder();
                sb.AppendLine("Id,Address,IOName,IsChecked,Note");
                foreach (var it in IOList)
                {
                    var idPart = it.Id;
                    var addrPart = (it.Address ?? "").Replace(",", "");
                    var namePart = (it.IOName ?? "").Replace(",", "");
                    var isChecked = it.IsChecked;
                    var note = "";
                    _notes.TryGetValue(GetKey(it), out note);
                    note = (note ?? "").Replace("\r", " ").Replace("\n", " ").Replace(",", "，"); // CSV 안전

                    sb.AppendLine($"{idPart},{addrPart},{namePart},{isChecked},{note}");
                }

                File.WriteAllText(_stateFile, sb.ToString(), Encoding.UTF8);
                MessageBox.Show($"저장 완료\n{_stateFile}", "저장", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"저장 중 오류: {ex.Message}", "저장 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadSavedStates(bool silent = false)
        {
            try
            {
                if (!File.Exists(_stateFile))
                {
                    if (!silent)
                        MessageBox.Show("저장된 체크 상태 파일이 없습니다.", "불러오기", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var lines = File.ReadAllLines(_stateFile, Encoding.UTF8);
                int appliedCheck = 0, appliedNote = 0;

                foreach (var line in lines.Skip(1))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    // 쉼표가 들어갈 수 있는 Note를 위해 1~4열까지만 Split, 나머지는 Note로 취급
                    var raw = line.Split(',');
                    if (raw.Length < 4) continue;

                    // 앞 4개는 고정
                    int.TryParse(raw[0], out var id);
                    var addr = (raw[1] ?? "").Trim();
                    var name = (raw[2] ?? "").Trim();
                    bool.TryParse(raw[3], out var isChecked);

                    // 5번째 이후는 Note 원문(과거 호환 위해 없을 수 있음)
                    string note = null;
                    if (raw.Length >= 5)
                        note = string.Join(",", raw.Skip(4)).Trim(); // CSV에서 ,를 ‘，’로 저장했으므로 그대로 결합

                    // 매칭
                    IOMonitorItem target = null;
                    if (id != 0)
                        target = IOList.FirstOrDefault(x => x.Id == id);

                    if (target == null && (!string.IsNullOrEmpty(addr) || !string.IsNullOrEmpty(name)))
                        target = IOList.FirstOrDefault(x =>
                            string.Equals(x.Address ?? "", addr, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(x.IOName ?? "", name, StringComparison.OrdinalIgnoreCase));

                    if (target != null)
                    {
                        target.IsChecked = isChecked;
                        appliedCheck++;

                        var key = GetKey(target);
                        if (!string.IsNullOrEmpty(note))
                        {
                            // 저장 시 개행/콤마 정리했으므로 그대로 탑재
                            _notes[key] = note;
                            appliedNote++;
                        }
                    }
                }

                if (!silent)
                    MessageBox.Show($"불러오기 완료: 체크 {appliedCheck}개, 메모 {appliedNote}개 적용\n{_stateFile}",
                        "불러오기", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                if (!silent)
                    MessageBox.Show($"불러오는 중 오류: {ex.Message}", "불러오기 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 탭 구성: DetailUnit 순서 보존 / 탭당 32개(좌16, 우16)
        private void RebuildTabsStable()
        {
            var oldKey = SelectedTab?.Key;

            var groupOrder = new List<string>();
            var bucket = new Dictionary<string, List<IOMonitorItem>>();

            foreach (var it in IOList) // DB 원본 순서 유지
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
                    var key = $"{detail}|{page / pageSize}";
                    var title = detail; // 헤더 = DetailUnit만
                    newTabs.Add(new TabPageVM(key, title, chunk));
                }
            }

            if (newTabs.Count == 0)
                newTabs.Add(new TabPageVM("EMPTY|0", "N/A", new List<IOMonitorItem>()));

            Tabs.Clear();
            foreach (var t in newTabs) Tabs.Add(t);

            SelectedTab = Tabs.FirstOrDefault(t => t.Key == oldKey) ?? Tabs.FirstOrDefault();
        }

        // 도움말 
        private void ShowHelp()
        {
            var msg =
@"I/O 모니터 사용법

• 저장/불러오기: Checked 상태와 메모(Note)을 CSV로 저장/적용.
• 탭: DetailUnit 순서 그대로, 한 탭에 32개(좌16/우16) 표시.
• Output: Y 주소(출력)만 토글 가능.
• Checked: 체크 시 행 배경이 강조됩니다.
• 메모: 각 행의 '메모' 버튼으로 메모 작성/수정/삭제.

저장 파일: " + StateFilePath;
            MessageBox.Show(msg, "도움말 — I/O Monitor", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // CC-Link 보드 래퍼/폴링 
        private void TryOpenBoard()
        {
            try
            {
                _hwConnected = (MdFunc32Wrapper.Open(_stationNo) == 0);
            }
            catch
            {
                _hwConnected = false;
            }
            OnPropertyChanged(nameof(HwStatusText));
            OnPropertyChanged(nameof(HwStatusBrush));
        }

        private bool ReadBitSafe(string address, out bool on)
        {
            on = false;
            if (string.IsNullOrWhiteSpace(address)) return false;

            if (!_hwConnected) TryOpenBoard();
            if (!_hwConnected) return false;

            try
            {
                return MdFunc32Wrapper.TryReadBit(_stationNo, address, out on);
            }
            catch
            {
                _hwConnected = false;
                OnPropertyChanged(nameof(HwStatusText));
                OnPropertyChanged(nameof(HwStatusBrush));
                return false;
            }
        }

        private bool WriteBitSafe(string address, bool value)
        {
            if (string.IsNullOrWhiteSpace(address)) return false;

            if (!_hwConnected) TryOpenBoard();
            if (!_hwConnected) return false;

            try
            {
                return MdFunc32Wrapper.TryWriteBit(_stationNo, address, value);
            }
            catch
            {
                _hwConnected = false;
                OnPropertyChanged(nameof(HwStatusText));
                OnPropertyChanged(nameof(HwStatusBrush));
                return false;
            }
        }

        private void PollVisibleItems()
        {
            if (SelectedTab == null || !_hwConnected) return;

            void Update(IEnumerable<IOMonitorItem> list)
            {
                foreach (var it in list)
                {
                    if (string.IsNullOrWhiteSpace(it.Address)) continue;
                    if (ReadBitSafe(it.Address, out var on))
                        it.CurrentState = on;
                }
            }

            try
            {
                Update(SelectedTab.Left16);
                Update(SelectedTab.Right16);
            }
            catch
            {
                // 폴링 중 예외는 무시(안전)
            }
        }

        // ★ 메모 키 (Id 우선, 없으면 Address|IOName)
        private static string GetKey(IOMonitorItem it)
        {
            if (it == null) return "";
            if (it.Id != 0) return $"ID:{it.Id}";
            var addr = it.Address ?? "";
            var name = it.IOName ?? "";
            return $"AK:{addr}|{name}";
        }
    }

    // 한 탭(페이지) 모델: 32개 아이템(좌16/우16)
    public class TabPageVM
    {
        public string Key { get; }
        public string Header { get; }
        public List<IOMonitorItem> Items { get; }

        public TabPageVM(string key, string header, List<IOMonitorItem> items)
        {
            Key = key;
            Header = header;
            Items = items ?? new List<IOMonitorItem>();
        }

        public IEnumerable<IOMonitorItem> Left16 => Items.Take(16);
        public IEnumerable<IOMonitorItem> Right16 => Items.Skip(16).Take(16);
    }
}
