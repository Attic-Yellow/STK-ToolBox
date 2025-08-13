using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using STK_ToolBox.Helpers;

namespace STK_ToolBox.ViewModels
{
    public sealed class InverterDriveMxViewModel : INotifyPropertyChanged, IDisposable
    {
        private MxComponentClient _plc;
        private InverterViaPlcController _inv;
        private CancellationTokenSource _cts;

        // ---- 설정 (MX 통신설정의 Logical Station 번호) ----
        private int _lsn = 1;                   // 예: 1
        public int LogicalStation { get => _lsn; set { _lsn = value; OnPropertyChanged(); } }

        public string RunCmdDevice { get; set; } = "RWr100";
        public string FreqCmdDevice { get; set; } = "RWr101";
        public string StatDevice { get; set; } = "RWw100";
        public string FreqMonDevice { get; set; } = "RWw101";
        private int _unit = 100;
        public int HzUnitDivisor { get => _unit; set { _unit = value; OnPropertyChanged(); if (_inv != null) _inv.HzUnitDivisor = value; } }

        // ---- 상태 ----
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

        // ---- 명령 ----
        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand RunFwdCommand { get; }
        public ICommand RunRevCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand SetFreqCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ShowHelpCommand { get; }

        public InverterDriveMxViewModel()
        {
            ConnectCommand = new RelayCommand(async () => await ConnectAsync(), () => !IsConnected);
            DisconnectCommand = new RelayCommand(() => Disconnect(), () => IsConnected);
            RunFwdCommand = new RelayCommand(async () => await RunAsync(true), () => IsConnected);
            RunRevCommand = new RelayCommand(async () => await RunAsync(false), () => IsConnected);
            StopCommand = new RelayCommand(async () => await StopAsync(), () => IsConnected);
            ResetCommand = new RelayCommand(async () => await ResetAsync(), () => IsConnected);
            SetFreqCommand = new RelayCommand(async () => await SetFreqAsync(), () => IsConnected);
            RefreshCommand = new RelayCommand(async () => await RefreshAsync(), () => IsConnected);
            ShowHelpCommand = new RelayCommand(() => ShowHelp());
        }

        private async Task ConnectAsync()
        {
            try
            {
                _cts?.Cancel(); _cts = new CancellationTokenSource();

                _plc = new MxComponentClient();
                if (!_plc.Open(LogicalStation, out string err))
                {
                    StatusText = err; IsConnected = false; return;
                }

                _inv = new InverterViaPlcController(_plc)
                {
                    RunCmdDevice = RunCmdDevice,
                    FreqCmdDevice = FreqCmdDevice,
                    StatDevice = StatDevice,
                    FreqMonDevice = FreqMonDevice,
                    HzUnitDivisor = HzUnitDivisor
                };

                IsConnected = true;
                StatusText = $"Connected (LSN={LogicalStation})";
                await RefreshAsync();
            }
            catch (Exception ex) { StatusText = "Connect exception: " + ex.Message; }
        }

        private void Disconnect()
        {
            try { _cts?.Cancel(); } catch { }
            try { _plc?.Close(); } catch { }
            _plc = null; _inv = null; IsConnected = false; StatusText = "Disconnected";
        }

        private async Task RunAsync(bool forward)
        {
            if (_inv == null) return;
            try { await _inv.SetRunAsync(forward, _cts.Token); await RefreshAsync(); }
            catch (Exception ex) { StatusText = "RUN FAIL: " + ex.Message; }
        }

        private async Task StopAsync()
        {
            if (_inv == null) return;
            try { await _inv.StopAsync(_cts.Token); await RefreshAsync(); }
            catch (Exception ex) { StatusText = "STOP FAIL: " + ex.Message; }
        }

        private async Task ResetAsync()
        {
            if (_inv == null) return;
            try { await _inv.ResetAsync(_cts.Token); await RefreshAsync(); }
            catch (Exception ex) { StatusText = "RESET FAIL: " + ex.Message; }
        }

        private async Task SetFreqAsync()
        {
            if (_inv == null) return;
            try { await _inv.SetFrequencyAsync(SetFreqHz, _cts.Token); await RefreshAsync(); }
            catch (Exception ex) { StatusText = "SET FREQ FAIL: " + ex.Message; }
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
            catch (Exception ex) { StatusText = "READ FAIL: " + ex.Message; }
        }

        private void ShowHelp()
        {
            const string msg =
@"● 사용 흐름 (MX Component 경로)
1) MX 통신설정 유틸에서 Logical Station 생성 (PLC=CC-Link Master, Ver.1)
2) 앱에서 LSN 입력 후 Connect
3) 디바이스: RWr/RWw 주소는 PLC의 CC-Link 마스터 버퍼메모리 주소
   예) RunCmd=RWr100, FreqCmd=RWr101, Stat=RWw100, FreqMon=RWw101
4) Set Freq → Run/Stop/Reset/Refresh

※ MX Component가 설치되어 있어야 합니다 (ActUtlType).";
            MessageBox.Show(msg, "도움말", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
        public void Dispose() => Disconnect();
    }
}
