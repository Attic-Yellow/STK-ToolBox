using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Management;

namespace STK_ToolBox.ViewModels
{
    public class PcInfo : INotifyPropertyChanged
    {
        private bool _isSelected;
        public bool IsSelected
        {
            get { return _isSelected; }
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public string Name { get; set; }
        public string Ip { get; set; }
        public string Status { get; set; }

        private string _remoteTime;
        public string RemoteTime
        {
            get { return _remoteTime; }
            set { _remoteTime = value; OnPropertyChanged(); }
        }

        private bool _hasCredential;
        public bool HasCredential
        {
            get => _hasCredential;
            set { _hasCredential = value; OnPropertyChanged(); OnPropertyChanged(nameof(CredentialButtonText)); }
        }

        private string _credentialUserDisplay;
        public string CredentialUserDisplay
        {
            get => _credentialUserDisplay;
            set { _credentialUserDisplay = value; OnPropertyChanged(); }
        }

        public string CredentialButtonText => HasCredential ? "변경" : "설정";

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propName = "")
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }

    public class TimeSyncViewModel : INotifyPropertyChanged
    {
        // 전역 기본 계정(개별 계정 없을 때 사용)
        public string AdminId { get; set; }
        public string AdminPassword { get; set; }

        // 범위 체크 (100단위)
        private bool _range0Checked = true;   // 0-99
        public bool Range0Checked
        {
            get => _range0Checked;
            set { _range0Checked = value; OnPropertyChanged(); }
        }

        private bool _range100Checked = true; // 100-199
        public bool Range100Checked
        {
            get => _range100Checked;
            set { _range100Checked = value; OnPropertyChanged(); }
        }

        private bool _range200Checked = true; // 200-254
        public bool Range200Checked
        {
            get => _range200Checked;
            set { _range200Checked = value; OnPropertyChanged(); }
        }

        // 스캔 상태
        private bool _isScanning;
        public bool IsScanning
        {
            get => _isScanning;
            private set { _isScanning = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotScanning)); }
        }
        public bool IsNotScanning => !IsScanning;

        // 진행률
        private int _scanProgress;
        public int ScanProgress
        {
            get => _scanProgress;
            set { _scanProgress = value; OnPropertyChanged(); }
        }

        public ObservableCollection<PcInfo> PcList { get; set; } = new ObservableCollection<PcInfo>();

        private PcInfo _selectedPc;
        public PcInfo SelectedPc
        {
            get => _selectedPc;
            set { _selectedPc = value; OnPropertyChanged(); }
        }

        private IList _selectedTargets;
        public IList SelectedTargets
        {
            get => _selectedTargets;
            set { _selectedTargets = value; OnPropertyChanged(); }
        }

        public ICommand SelectAllCommand { get; }
        public ICommand DeselectAllCommand { get; }
        public ICommand ScanCommand { get; }
        public ICommand SyncCommand { get; }

        // 설정 가능한 서브넷(필요시 변경)
        private const string SubnetPrefix = "192.168.10.";

        // 자격증명 저장소
        private readonly CredentialStore _credentialStore = new CredentialStore();

        public TimeSyncViewModel()
        {
            // 동기화 커맨드(메인PC 선택 여부에 따라)
            SyncCommand = new RelayCommand(SyncTimeSelected, () => SelectedPc != null);

            SelectAllCommand = new RelayCommand(() =>
            {
                foreach (var pc in PcList) pc.IsSelected = true;
            }, () => PcList.Any());

            DeselectAllCommand = new RelayCommand(() =>
            {
                foreach (var pc in PcList) pc.IsSelected = false;
            }, () => PcList.Any());

            // Scan 버튼을 눌렀을 때만 스캔
            ScanCommand = new RelayCommand(async () => await ScanSelectedRangesAsync(), () => IsNotScanning);

            // 로컬 PC 먼저(선택 사항)
            AddLocalPc();
            // 저장소 로드
            _credentialStore.Load();
            // 로컬 PC에 자격 플래그 반영
            foreach (var pc in PcList) RefreshCredentialIndicators(pc.Ip);
        }

        public void SetCredentialForIp(string ip, string user, string password)
        {
            _credentialStore.Set(ip, user, password);
            _credentialStore.Save();
        }

        public void RefreshCredentialIndicators(string ip)
        {
            var found = PcList.FirstOrDefault(p => p.Ip == ip);
            if (found != null)
            {
                var cred = _credentialStore.Get(ip);
                found.HasCredential = cred != null;
                found.CredentialUserDisplay = cred != null ? cred.UserName : "미설정";
            }
        }

        private void AddLocalPc()
        {
            try
            {
                string hostName = Dns.GetHostName();

                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus == OperationalStatus.Up &&
                        ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    {
                        foreach (var addr in ni.GetIPProperties().UnicastAddresses)
                        {
                            if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            {
                                string ip = addr.Address.ToString();
                                if (ip.StartsWith(SubnetPrefix))
                                {
                                    PcList.Add(new PcInfo
                                    {
                                        Name = hostName + " (Local)",
                                        Ip = ip,
                                        Status = "본인 PC",
                                        RemoteTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // 무시
            }
        }

        private Tuple<int, int>[] GetSelectedRanges()
        {
            var ranges = new System.Collections.Generic.List<Tuple<int, int>>();
            if (Range0Checked) ranges.Add(Tuple.Create(1, 99));      // 1~99
            if (Range100Checked) ranges.Add(Tuple.Create(100, 199));
            if (Range200Checked) ranges.Add(Tuple.Create(200, 254)); // 255 제외
            return ranges.ToArray();
        }

        private async Task ScanSelectedRangesAsync()
        {
            var ranges = GetSelectedRanges();
            if (ranges.Length == 0)
            {
                MessageBox.Show("스캔할 범위를 선택하세요. (0–99, 100–199, 200–254 중 하나 이상)");
                return;
            }

            IsScanning = true;
            ScanProgress = 0;

            // 기존 목록 초기화
            PcList.Clear();
            AddLocalPc();

            // 저장소 최신 로드
            _credentialStore.Load();

            int total = ranges.Sum(r => r.Item2 - r.Item1 + 1);
            int completed = 0;

            var throttler = new SemaphoreSlim(64);
            var tasks = new System.Collections.Generic.List<Task>();

            foreach (var r in ranges)
            {
                for (int i = r.Item1; i <= r.Item2; i++)
                {
                    string ip = SubnetPrefix + i;
                    await throttler.WaitAsync();
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            using (var ping = new Ping())
                            {
                                var reply = await ping.SendPingAsync(ip, 200);
                                if (reply.Status == IPStatus.Success)
                                {
                                    string name = ip;
                                    try
                                    {
                                        var host = Dns.GetHostEntry(ip);
                                        if (!string.IsNullOrWhiteSpace(host.HostName))
                                            name = host.HostName;
                                    }
                                    catch { }

                                    string time = "읽기 실패";
                                    try
                                    {
                                        var opts = GetConnectionOptionsForIp(ip);
                                        if (opts != null)
                                        {
                                            var scope = new ManagementScope($@"\\{ip}\root\cimv2", opts);
                                            scope.Connect();

                                            var query = new ObjectQuery("SELECT LocalDateTime FROM Win32_OperatingSystem");
                                            using (var searcher = new ManagementObjectSearcher(scope, query))
                                            {
                                                foreach (ManagementObject os in searcher.Get())
                                                {
                                                    var current = ManagementDateTimeConverter.ToDateTime(os["LocalDateTime"].ToString());
                                                    time = current.ToString("yyyy-MM-dd HH:mm:ss");
                                                }
                                            }
                                        }
                                    }
                                    catch
                                    {
                                        // 자격증명 실패 시 읽기 실패
                                    }

                                    App.Current.Dispatcher.Invoke(() =>
                                    {
                                        var item = new PcInfo
                                        {
                                            Name = name,
                                            Ip = ip,
                                            Status = "연결됨",
                                            RemoteTime = time
                                        };
                                        PcList.Add(item);
                                        // 자격 플래그 반영
                                        RefreshCredentialIndicators(ip);
                                    });
                                }
                            }
                        }
                        catch
                        {
                            // 무시
                        }
                        finally
                        {
                            Interlocked.Increment(ref completed);
                            ScanProgress = (int)((completed / (double)total) * 100);
                            throttler.Release();
                        }
                    }));
                }
            }

            await Task.WhenAll(tasks);
            ScanProgress = 100;
            IsScanning = false;
        }

        private ConnectionOptions GetConnectionOptionsForIp(string ip)
        {
            // 1) IP별 저장 자격증명
            var cred = _credentialStore.Get(ip);
            if (cred != null)
            {
                return new ConnectionOptions
                {
                    Username = cred.UserName,
                    Password = cred.GetPasswordPlain(),
                    EnablePrivileges = true,
                    Impersonation = ImpersonationLevel.Impersonate
                };
            }

            // 2) 전역 기본 계정(설정되어 있으면)
            if (!string.IsNullOrWhiteSpace(AdminId))
            {
                return new ConnectionOptions
                {
                    Username = AdminId,
                    Password = AdminPassword ?? "",
                    EnablePrivileges = true,
                    Impersonation = ImpersonationLevel.Impersonate
                };
            }

            // 자격 없음 -> null (WMI 연결 시도하지 않음)
            return null;
        }

        public void SyncTimeSelected()
        {
            if (SelectedPc == null)
            {
                MessageBox.Show("메인 PC를 선택하세요.");
                return;
            }

            var targets = PcList.Where(p => p.IsSelected && p.Ip != SelectedPc.Ip).ToList();
            if (targets.Count == 0)
            {
                MessageBox.Show("동기화할 PC를 선택하세요.");
                return;
            }

            DateTime mainTime = DateTime.Now; // 메인 PC 시간 기준

            foreach (var pc in targets)
            {
                SyncTime(pc, mainTime);
            }

            MessageBox.Show("선택된 PC 시간 동기화 완료!");
        }

        private void SyncTime(PcInfo target, DateTime time)
        {
            try
            {
                var options = GetConnectionOptionsForIp(target.Ip);
                if (options == null)
                    throw new Exception("자격증명이 없습니다. (IP별 계정 설정 또는 기본 계정을 입력하세요)");

                var scope = new ManagementScope($@"\\{target.Ip}\root\cimv2", options);
                scope.Connect();

                var path = new ManagementPath("Win32_OperatingSystem=@");
                using (var os = new ManagementObject(scope, path, null))
                {
                    var inParams = os.GetMethodParameters("SetDateTime");
                    inParams["LocalDateTime"] = ManagementDateTimeConverter.ToDmtfDateTime(time);
                    os.InvokeMethod("SetDateTime", inParams, null);
                }

                target.Status = "동기화 성공";
                target.RemoteTime = time.ToString("yyyy-MM-dd HH:mm:ss");
            }
            catch (Exception ex)
            {
                target.Status = "동기화 실패";
                target.RemoteTime = "설정 실패";
                MessageBox.Show($"{target.Ip} 동기화 실패: {ex.Message}");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propName = "")
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }

    // RelayCommand (매개변수 없는 간단형)
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;
        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }
        public bool CanExecute(object parameter) => _canExecute == null || _canExecute();
        public void Execute(object parameter) => _execute();
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}
