using STK_ToolBox.Helpers;
using STK_ToolBox.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace STK_ToolBox.ViewModels
{
    /// <summary>
    /// IO 모니터 공통 기능 베이스:
    /// - IOList 관리
    /// - CC-Link 보드 Open / Polling
    /// - Y 출력 토글(Turn ON/OFF)
    /// - 체크/메모 저장/불러오기 (+ 자동 저장)
    /// - HwStatusText / HwStatusBrush
    /// IOCheckViewModel, InverterViewModel 등이 상속해서 사용.
    /// </summary>
    public abstract class IoMonitorViewModelBase : BaseViewModel
    {
        //  Public Bindings 

        public ObservableCollection<IOMonitorItem> IOList { get; private set; }

        protected bool _hwConnected;
        public string HwStatusText
        {
            get { return _hwConnected ? "H/W: Connected" : "H/W: Disconnected"; }
        }

        public Brush HwStatusBrush
        {
            get { return _hwConnected ? Brushes.SeaGreen : Brushes.IndianRed; }
        }

        public string StateFilePath
        {
            get { return _stateFile; }
        }

        public ICommand SaveCommand { get; private set; }
        public ICommand LoadStateCommand { get; private set; }
        public ICommand ToggleOutputCommand { get; private set; }
        public ICommand OpenNoteCommand { get; private set; }

        //  내부 필드 

        protected readonly short _channelNo;

        protected readonly string _stateDir;
        protected readonly string _stateFile;

        protected readonly Dictionary<string, string> _notes =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // 변경 추적용
        private long _nextVersion = 1;        // 변경 버전 증가용
        private long _lastSavedVersion = 0;   // 마지막으로 저장한 버전
        private DateTime _lastSaveTime = DateTime.MinValue;  // 마지막 저장 시각

        /// <summary>주소 파싱 결과 캐시</summary>
        protected class ParsedAddr
        {
            public short DevType;     // MdFunc32Wrapper.DevX / DevY
            public short Station;     // 1..n
            public int Bit;           // 0..31
            public short BlockStart;  // 0 또는 16
            public int BitOffset;     // 0..15
            public bool IsOutput
            {
                get { return DevType == MdFunc32Wrapper.DevY; }
            }
        }

        protected readonly Dictionary<IOMonitorItem, ParsedAddr> _addrCache =
            new Dictionary<IOMonitorItem, ParsedAddr>();

        protected readonly DispatcherTimer _pollTimer;
        protected bool _polling;

        // 자동 저장 관련
        private readonly DispatcherTimer _autoSaveTimer;
        private bool _autoSavePending;
        private bool _suppressAutoSave;

        //  생성자 
        protected IoMonitorViewModelBase(short channelNo, string stateFileName)
        {
            _channelNo = channelNo;

            IOList = new ObservableCollection<IOMonitorItem>();
            IOList.CollectionChanged += IOList_CollectionChanged;

            _stateDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "STK_ToolBox");
            _stateFile = Path.Combine(_stateDir, stateFileName);

            SaveCommand = new RelayCommand(new Action(SaveStates));
            LoadStateCommand = new RelayCommand(new Action(LoadSavedStates));
            ToggleOutputCommand = new RelayCommand<IOMonitorItem>(ToggleOutput, CanToggleOutput);
            OpenNoteCommand = new RelayCommand<IOMonitorItem>(OpenNote);

            // Poll timer
            _pollTimer = new DispatcherTimer();
            _pollTimer.Interval = TimeSpan.FromMilliseconds(500);
            _pollTimer.Tick += async (s, e) => await PollVisibleItemsAsync();
            _pollTimer.Start();

            // Auto save timer (디바운스용)
            _autoSaveTimer = new DispatcherTimer();
            _autoSaveTimer.Interval = TimeSpan.FromSeconds(1.5); // 마지막 변경 후 1.5초 지나면 저장 시도
            _autoSaveTimer.Tick += AutoSaveTimer_Tick;
        }

        //  View 로드 시 보드 Open 
        public virtual void OnViewLoaded()
        {
            short rc = MdFunc32Wrapper.Open(_channelNo);
            _hwConnected = (rc == 0);
            OnPropertyChanged("HwStatusText");
            OnPropertyChanged("HwStatusBrush");

            if (!_hwConnected)
            {
                PostPopup(
                    "로드 오류: mdOpen 실패(chan=" + _channelNo + ", rc=" + rc + ")",
                    "오류",
                    MessageBoxImage.Error);
            }

            var _ = PollVisibleItemsAsync();
        }

        //  공통 팝업 
        protected void PostPopup(string text, string title, MessageBoxImage icon)
        {
            var d = Application.Current != null ? Application.Current.Dispatcher : null;
            if (d == null) return;

            d.BeginInvoke(
                new Action(delegate { MessageBox.Show(text, title, MessageBoxButton.OK, icon); }),
                DispatcherPriority.Background);
        }

        //  메모/노트 
        protected virtual void OpenNote(IOMonitorItem item)
        {
            if (item == null) return;

            string key = GetKey(item);
            string current;
            _notes.TryGetValue(key, out current);

            var result = NotePrompt.Show(
                "특이사항 코멘트 입력창",
                "해당 I/O 항목에 대한 메모를 입력하세요.\r\n빈 칸으로 저장하면 메모가 삭제됩니다.",
                current ?? string.Empty);

            if (result == null) return;

            var text = result.Trim();
            if (string.IsNullOrEmpty(text))
                _notes.Remove(key);
            else
                _notes[key] = text;

            // 메모 수정도 자동 저장 대상
            MarkDirtyAndScheduleSave();
        }

        //  출력 토글 
        protected virtual bool CanToggleOutput(IOMonitorItem item)
        {
            return item != null && item.CanToggle;
        }

        protected virtual void ToggleOutput(IOMonitorItem item)
        {
            if (item == null)
            {
                PostPopup("선택된 I/O 항목이 없습니다.", "I/O 출력", MessageBoxImage.Warning);
                return;
            }

            if (!item.CanToggle)
            {
                PostPopup(
                    "이 항목은 출력(Y)가 아니거나 토글 불가로 설정되어 있습니다.\r\nAddress=" + item.Address,
                    "I/O 출력",
                    MessageBoxImage.Warning);
                return;
            }

            if (!_hwConnected)
            {
                PostPopup(
                    "보드가 연결되지 않았습니다.\r\n(H/W: Disconnected 상태)",
                    "I/O 출력",
                    MessageBoxImage.Warning);
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
                // 필요하면 토글도 자동 저장 대상으로 포함할 수 있음
                // MarkDirtyAndScheduleSave();
            }
            else
            {
                PostPopup("출력 토글 실패(Write 실패): " + item.Address,
                          "I/O 출력", MessageBoxImage.Warning);
            }
        }

        //  체크/메모 저장/불러오기 

        // 수동 저장(버튼) → 팝업 표시 + 버전 동기화
        protected virtual void SaveStates()
        {
            SaveStatesCore(true);
            _lastSavedVersion = _nextVersion;
            _lastSaveTime = DateTime.Now;
        }

        // 실제 저장 로직 (showPopup=false 이면 자동 저장용)
        private void SaveStatesCore(bool showPopup)
        {
            try
            {
                if (!Directory.Exists(_stateDir))
                    Directory.CreateDirectory(_stateDir);

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
                    note = (note ?? "")
                        .Replace("\r", " ")
                        .Replace("\n", " ")
                        .Replace(",", "，");

                    sb.AppendLine(idPart + "," + addrPart + "," + namePart + "," + isChecked + "," + note);
                }

                File.WriteAllText(_stateFile, sb.ToString(), Encoding.UTF8);

                if (showPopup)
                    PostPopup("저장 완료\r\n" + _stateFile, "저장", MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                PostPopup("저장 중 오류: " + ex.Message, "저장 오류", MessageBoxImage.Error);
            }
        }

        protected void LoadSavedStates()
        {
            LoadSavedStates(false);
        }

        protected virtual void LoadSavedStates(bool silent)
        {
            try
            {
                if (!File.Exists(_stateFile))
                {
                    if (!silent)
                        PostPopup("저장된 체크 상태 파일이 없습니다.",
                                  "불러오기",
                                  MessageBoxImage.Information);
                    return;
                }

                _suppressAutoSave = true;   // 복원 중에는 자동 저장 막기

                var lines = File.ReadAllLines(_stateFile, Encoding.UTF8);
                int appliedCheck = 0;
                int appliedNote = 0;

                foreach (var line in lines.Skip(1))
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var raw = line.Split(',');
                    if (raw.Length < 4) continue;

                    int id;
                    int.TryParse(raw[0], out id);
                    string addr = (raw[1] ?? "").Trim();
                    string name = (raw[2] ?? "").Trim();
                    bool isChecked;
                    bool.TryParse(raw[3], out isChecked);

                    string note = raw.Length >= 5
                        ? string.Join(",", raw.Skip(4)).Trim()
                        : null;

                    IOMonitorItem target = null;
                    if (id != 0)
                        target = IOList.FirstOrDefault(x => x.Id == id);

                    if (target == null &&
                        (!string.IsNullOrEmpty(addr) || !string.IsNullOrEmpty(name)))
                    {
                        target = IOList.FirstOrDefault(x =>
                            string.Equals(x.Address ?? "", addr, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(x.IOName ?? "", name, StringComparison.OrdinalIgnoreCase));
                    }

                    if (target != null)
                    {
                        target.IsChecked = isChecked;
                        appliedCheck++;

                        string key = GetKey(target);
                        if (!string.IsNullOrEmpty(note))
                        {
                            _notes[key] = note;
                            appliedNote++;
                        }
                    }
                }

                // 로드가 끝난 시점의 상태를 "저장된 상태"로 간주
                _lastSavedVersion = _nextVersion;
                _lastSaveTime = DateTime.Now;

                if (!silent)
                {
                    PostPopup(
                        "불러오기 완료: 체크 " + appliedCheck + "개, 메모 " + appliedNote + "개 적용\r\n" +
                        _stateFile,
                        "불러오기",
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                if (!silent)
                {
                    PostPopup("불러오는 중 오류: " + ex.Message,
                              "불러오기 오류",
                              MessageBoxImage.Error);
                }
            }
            finally
            {
                _suppressAutoSave = false;
            }
        }

        //  주소 캐시 구성 
        protected virtual void BuildAddressCache()
        {
            _addrCache.Clear();

            foreach (var it in IOList)
            {
                if (string.IsNullOrWhiteSpace(it.Address))
                    continue;

                short devType;
                short st;
                int bit;

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

        //  Polling 
        protected abstract IEnumerable<IOMonitorItem> GetVisibleItems();

        protected virtual async Task PollVisibleItemsAsync()
        {
            if (!_hwConnected) return;
            if (_polling) return;
            _polling = true;

            try
            {
                var visible = GetVisibleItems()
                    .Where(it => it != null && _addrCache.ContainsKey(it))
                    .ToList();

                if (visible.Count == 0) return;

                var groups = visible.GroupBy(it =>
                {
                    ParsedAddr p = _addrCache[it];
                    return p.DevType + ":" + p.Station + ":" + p.BlockStart;
                }).ToList();

                var results = new Dictionary<IOMonitorItem, bool>();

                await Task.Run(delegate
                {
                    foreach (var g in groups)
                    {
                        ParsedAddr first = _addrCache[g.First()];
                        ushort bits;
                        if (!MdFunc32Wrapper.TryReadBlock16(first.Station, first.DevType, first.BlockStart, out bits))
                            continue;

                        foreach (var it in g)
                        {
                            ParsedAddr p = _addrCache[it];
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

        //  자동 저장 관련 유틸 
        private void IOList_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (var obj in e.NewItems)
                {
                    var it = obj as IOMonitorItem;
                    if (it != null)
                    {
                        it.PropertyChanged += Item_PropertyChanged;
                    }
                }
            }

            if (e.OldItems != null)
            {
                foreach (var obj in e.OldItems)
                {
                    var it = obj as IOMonitorItem;
                    if (it != null)
                    {
                        it.PropertyChanged -= Item_PropertyChanged;
                    }
                }
            }
            // Reset 등은 개별 PropertyChanged에서 처리
        }

        private void Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_suppressAutoSave) return;

            // 체크박스 변경 시 자동 저장 예약
            if (e.PropertyName == "IsChecked")
            {
                MarkDirtyAndScheduleSave();
            }
            // 메모는 OpenNote에서 MarkDirtyAndScheduleSave 호출
        }

        private void MarkDirtyAndScheduleSave()
        {
            _nextVersion++;              // 변경 버전 증가
            _autoSavePending = true;

            // 디바운스: 타이머 시작 (이미 켜져 있으면 Tick 시점에 한 번만 처리)
            if (!_autoSaveTimer.IsEnabled)
                _autoSaveTimer.Start();
        }

        private void AutoSaveTimer_Tick(object sender, EventArgs e)
        {
            _autoSaveTimer.Stop();

            if (!_autoSavePending)
                return;

            _autoSavePending = false;

            // 변경된 게 없으면 저장 안 함
            if (_nextVersion == _lastSavedVersion)
                return;

            // 최소 저장 간격 (예: 30초) 보다 너무 자주면 스킵
            TimeSpan minInterval = TimeSpan.FromSeconds(30);
            if (DateTime.Now - _lastSaveTime < minInterval)
            {
                // 다음 변경 때 다시 시도하도록만 표시
                _autoSavePending = true;
                _autoSaveTimer.Start();
                return;
            }

            // 조용히 자동 저장 (팝업 없음)
            SaveStatesCore(false);
            _lastSavedVersion = _nextVersion;
            _lastSaveTime = DateTime.Now;
        }

        //  유틸 
        protected static string GetKey(IOMonitorItem it)
        {
            if (it == null) return "";
            if (it.Id != 0) return "ID:" + it.Id;
            string addr = it.Address ?? "";
            string name = it.IOName ?? "";
            return "AK:" + addr + "|" + name;
        }
    }
}
