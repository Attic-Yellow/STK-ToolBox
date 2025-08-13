using STK_ToolBox.Helpers;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace STK_ToolBox.ViewModels
{
    public sealed class InverterDriveCCLinkViewModel : INotifyPropertyChanged, IDisposable
    {
        // -------- 내부 --------
        private CCLinkClient _cli;
        private InverterCcLinkController _inv;
        private CancellationTokenSource _cts;

        // -------- 보드 목록 / 선택 (드롭다운) --------
        public class BoardItem
        {
            public short No { get; set; }
            public string Name { get; set; }   // 콤보박스에 보일 짧은 텍스트 (예: "Board 0 (OK)")
            public string Tip { get; set; }    // 툴팁에만 보일 상세(선택)
            public override string ToString() => Name;
        }

        public ObservableCollection<BoardItem> Boards { get; } = new ObservableCollection<BoardItem>();
        private BoardItem _selectedBoard;
        public BoardItem SelectedBoard
        {
            get => _selectedBoard;
            set
            {
                _selectedBoard = value; OnPropertyChanged();
                if (value != null) BoardNo = value.No;
            }
        }

        private short _boardNo = 0;
        public short BoardNo { get => _boardNo; set { _boardNo = value; OnPropertyChanged(); } }

        // -------- 설정 --------
        private short _stationNo = 1;
        public short StationNo
        {
            get => _stationNo;
            set { _stationNo = value; OnPropertyChanged(); if (_inv != null) _inv.StationNo = value; }
        }

        private short _run = 100, _fcmd = 101, _stat = 100, _fmon = 101;
        public short RunCmdAddr { get => _run; set { _run = value; OnPropertyChanged(); if (_inv != null) _inv.RunCmdAddr = value; } }
        public short FreqCmdAddr { get => _fcmd; set { _fcmd = value; OnPropertyChanged(); if (_inv != null) _inv.FreqCmdAddr = value; } }
        public short StatAddr { get => _stat; set { _stat = value; OnPropertyChanged(); if (_inv != null) _inv.StatAddr = value; } }
        public short FreqMonAddr { get => _fmon; set { _fmon = value; OnPropertyChanged(); if (_inv != null) _inv.FreqMonAddr = value; } }

        private int _unit = 100; // 0.01Hz
        public int HzUnitDivisor
        {
            get => _unit;
            set { _unit = value; OnPropertyChanged(); if (_inv != null) _inv.HzUnitDivisor = value; }
        }

        // -------- 상태 --------
        private bool _isConnected;
        public bool IsConnected { get => _isConnected; private set { _isConnected = value; OnPropertyChanged(); } }

        private string _statusText = "Disconnected";
        public string StatusText { get => _statusText; private set { _statusText = value; OnPropertyChanged(); } }

        private double _setFreqHz = 60.0;
        public double SetFreqHz { get => _setFreqHz; set { _setFreqHz = value; OnPropertyChanged(); } }

        private double _curFreqHz;
        public double CurFreqHz { get => _curFreqHz; private set { _curFreqHz = value; OnPropertyChanged(); } }

        private int _statusWord;
        public int StatusWord { get => _statusWord; private set { _statusWord = value; OnPropertyChanged(); } }

        // -------- 명령 --------
        public ICommand DetectBoardsCommand { get; }
        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand RunFwdCommand { get; }
        public ICommand RunRevCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand SetFreqCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ShowHelpCommand { get; }

        public InverterDriveCCLinkViewModel()
        {
            DetectBoardsCommand = new RelayCommand(async () => await DetectBoardsAsync());
            ConnectCommand = new RelayCommand(async () => await ConnectAsync(), () => !IsConnected);
            DisconnectCommand = new RelayCommand(() => Disconnect(), () => IsConnected);
            RunFwdCommand = new RelayCommand(async () => await RunAsync(true), () => IsConnected);
            RunRevCommand = new RelayCommand(async () => await RunAsync(false), () => IsConnected);
            StopCommand = new RelayCommand(async () => await StopAsync(), () => IsConnected);
            ResetCommand = new RelayCommand(async () => await ResetAsync(), () => IsConnected);
            SetFreqCommand = new RelayCommand(async () => await SetFreqAsync(), () => IsConnected);
            RefreshCommand = new RelayCommand(async () => await RefreshAsync(), () => IsConnected);
            ShowHelpCommand = new RelayCommand(() => ShowHelp());

            // 시작 시 한 번 자동 감지
            _ = DetectBoardsAsync();
        }

        // -------- 자동 감지 --------
        private async Task DetectBoardsAsync()
        {
            Boards.Clear();

            for (short i = 0; i <= 7; i++)
            {
                try
                {
                    using (var cli = new CCLinkClient())
                    {
                        if (cli.Open(i, out string err))
                        {
                            // UI는 짧게, 상세는 로그에만
                            Log.Info($"Board {i} Open OK");
                            Boards.Add(new BoardItem
                            {
                                No = i,
                                Name = $"Board {i} (OK)",
                                Tip = $"Board {i} Open OK"
                            });
                            cli.Close();
                        }
                        else
                        {
                            Log.Warn($"Board {i} Open NG: {err}");
                            Boards.Add(new BoardItem
                            {
                                No = i,
                                Name = $"Board {i} (NG)",
                                Tip = $"Open NG: {err}"   // 툴팁에서만 볼 수 있게
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Board {i} open exception");
                    Boards.Add(new BoardItem
                    {
                        No = i,
                        Name = $"Board {i} (NG)",
                        Tip = $"Exception: {ErrorShortener.Short(ex.Message)}"
                    });
                }

                await Task.Delay(50);
            }

            // 자동 선택: 첫 OK 보드, 없으면 첫 항목
            var ok = Boards.FirstOrDefault(b => b.Name.Contains("(OK)"));
            SelectedBoard = ok ?? (Boards.Count > 0 ? Boards[0] : null);

            StatusText = ok != null ? $"보드 자동 선택: {ok.No}" : "열 수 있는 보드 없음";
        }


        // -------- 연결/제어 --------
        private async Task ConnectAsync()
        {
            try
            {
                _cts?.Cancel();
                _cts = new CancellationTokenSource();

                _cli = new CCLinkClient();
                if (!_cli.Open(BoardNo, out string err))
                {
                    StatusText = "Open fail: " + err;
                    ShowErrorIfCritical(err);
                    IsConnected = false;
                    return;
                }

                _inv = new InverterCcLinkController(_cli)
                {
                    StationNo = StationNo,
                    RunCmdAddr = RunCmdAddr,
                    FreqCmdAddr = FreqCmdAddr,
                    StatAddr = StatAddr,
                    FreqMonAddr = FreqMonAddr,
                    HzUnitDivisor = HzUnitDivisor
                };

                IsConnected = true;
                StatusText = $"Connected (Board={BoardNo}, Station={StationNo})";
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                StatusText = "Connect exception: " + ex.Message;
                ShowErrorIfCritical(ex.Message);
            }
        }

        private void Disconnect()
        {
            try { _cts?.Cancel(); } catch { }
            try { _cli?.Close(); } catch { }
            _cli = null; _inv = null;
            IsConnected = false;
            StatusText = "Disconnected";
        }

        private async Task RunAsync(bool forward)
        {
            if (_inv == null) return;
            try { await _inv.SetRunAsync(forward, _cts.Token); await RefreshAsync(); }
            catch (Exception ex) { StatusText = "Run fail: " + ex.Message; }
        }

        private async Task StopAsync()
        {
            if (_inv == null) return;
            try { await _inv.StopAsync(_cts.Token); await RefreshAsync(); }
            catch (Exception ex) { StatusText = "Stop fail: " + ex.Message; }
        }

        private async Task ResetAsync()
        {
            if (_inv == null) return;
            try { await _inv.ResetAsync(_cts.Token); await RefreshAsync(); }
            catch (Exception ex) { StatusText = "Reset fail: " + ex.Message; }
        }

        private async Task SetFreqAsync()
        {
            if (_inv == null) return;
            try { await _inv.SetFrequencyAsync(SetFreqHz, _cts.Token); await RefreshAsync(); }
            catch (Exception ex) { StatusText = "SetFreq fail: " + ex.Message; }
        }

        private async Task RefreshAsync()
        {
            if (_inv == null) return;
            try
            {
                var (st, hz) = await _inv.ReadStatusAsync(_cts.Token);
                StatusWord = st; CurFreqHz = hz;
                StatusText = $"Status=0x{st:X4}, Freq={hz:0.00} Hz";
            }
            catch (Exception ex) { StatusText = "Read fail: " + ex.Message; }
        }

        // -------- 도움말/에러 --------
        private void ShowErrorIfCritical(string msg)
        {
            if (msg.IndexOf("DLL", StringComparison.OrdinalIgnoreCase) >= 0 ||
                msg.IndexOf("x86", StringComparison.OrdinalIgnoreCase) >= 0 ||
                msg.IndexOf("플랫폼", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                MessageBox.Show(
                    msg + "\n\n해결:\n - SW1DNC-CCBD2-B SDK 설치\n - 프로젝트 x86 빌드\n - D:\\STK ToolBox 에 DLL(의존 DLL 포함) 배치",
                    "CC-Link DLL 로드 실패", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowHelp()
        {
            const string text =
@"● 사용 순서
1) [보드 검색]으로 사용 가능한 보드 자동 스캔 → 드롭다운에서 선택
2) Station(슬레이브 스테이션) 입력 → Connect
3) 주소맵/Hz Unit 확인
4) Set Freq → Run FWD/REV/Stop/Reset → Refresh

● Master Station Ver.1 모드
- 보드 설정 유틸에서 Ver.1, 전송속도, 네트워크/국번 설정 후 저장하세요.

● 문제 해결
- DLL 로드 실패 → D:\STK ToolBox 에 SDK Bin 전체 복사, x86 빌드
- 보드 Open 실패 → 장치관리자 확인, 보드 파라미터/케이블/전원 확인";
            MessageBox.Show(text, "인버터 제어 도움말", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // -------- INotifyPropertyChanged / IDisposable --------
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
        public void Dispose() => Disconnect();
    }
}
