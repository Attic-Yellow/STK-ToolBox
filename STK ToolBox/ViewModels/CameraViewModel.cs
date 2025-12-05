using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using STK_ToolBox.Helpers; // RelayCommand

namespace STK_ToolBox.ViewModels
{
    #region CameraItem (단일 카메라 모델)

    public class CameraItem : INotifyPropertyChanged
    {
        #region Fields

        private bool _isOnline;
        private bool _isSelected;

        #endregion

        #region Properties

        public string Ip { get; set; }

        public bool IsOnline
        {
            get { return _isOnline; }
            set
            {
                if (_isOnline != value)
                {
                    _isOnline = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 필요 시 MJPEG/RTSP 경로를 지정해서 힌트로 노출할 수 있음
        /// </summary>
        public string StreamUrl { get; set; }

        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string p = null)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(p));
        }

        #endregion
    }

    #endregion

    #region CameraViewModel (CCTV 목록 + 스캔/브라우저 연동)

    public class CameraViewModel : INotifyPropertyChanged
    {
        #region Fields

        private CancellationTokenSource _scanCts;

        private string _username;
        private string _password;
        private string _playerStatus = "Ready";
        private CameraItem _selectedCamera;

        public ObservableCollection<CameraItem> Cameras { get; private set; }

        private ICollectionView _camerasView;

        #endregion

        #region View / Binding Properties

        public ICollectionView CamerasView
        {
            get { return _camerasView; }
            private set
            {
                _camerasView = value;
                OnPropertyChanged("CamerasView");
            }
        }

        public string Username
        {
            get { return _username; }
            set
            {
                if (_username != value)
                {
                    _username = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Password
        {
            get { return _password; }
            set
            {
                if (_password != value)
                {
                    _password = value;
                    OnPropertyChanged();
                }
            }
        }

        public string PlayerStatus
        {
            get { return _playerStatus; }
            set
            {
                if (_playerStatus != value)
                {
                    _playerStatus = value;
                    OnPropertyChanged();
                }
            }
        }

        public CameraItem SelectedCamera
        {
            get { return _selectedCamera; }
            set
            {
                if (_selectedCamera != value)
                {
                    _selectedCamera = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion

        #region Commands

        public ICommand ScanCommand { get; private set; }
        public ICommand CancelScanCommand { get; private set; }
        public ICommand SelectAllCommand { get; private set; }
        public ICommand ClearSelectionCommand { get; private set; }
        public ICommand OpenSelectedInBrowserCommand { get; private set; }

        #endregion

        #region Constructor

        public CameraViewModel()
        {
            Cameras = new ObservableCollection<CameraItem>();

            // 컬렉션뷰: 온라인만 필터
            CamerasView = CollectionViewSource.GetDefaultView(Cameras);
            CamerasView.Filter = o =>
            {
                var cam = o as CameraItem;
                return cam != null && cam.IsOnline;
            };

            Cameras.CollectionChanged += Cameras_CollectionChanged;

            ScanCommand = new RelayCommand(async () => await ScanAsync(), () => _scanCts == null);
            CancelScanCommand = new RelayCommand(() => CancelScan(), () => _scanCts != null);

            // 보이는 항목만 대상으로 전체선택/해제
            SelectAllCommand = new RelayCommand(() =>
            {
                foreach (var cam in CamerasView.Cast<object>().OfType<CameraItem>())
                    cam.IsSelected = true;
            }, () => CamerasView != null && CamerasView.Cast<object>().OfType<CameraItem>().Any());

            ClearSelectionCommand = new RelayCommand(() =>
            {
                foreach (var cam in CamerasView.Cast<object>().OfType<CameraItem>())
                    cam.IsSelected = false;
            }, () => CamerasView != null && CamerasView.Cast<object>().OfType<CameraItem>().Any());

            OpenSelectedInBrowserCommand =
                new RelayCommand(() => OpenSelectedInBrowser(),
                                 () => Cameras.Any(c => c.IsOnline && c.IsSelected));
        }

        #endregion

        #region Scan Logic

        private async Task ScanAsync()
        {
            // 기존 스캔 있으면 취소
            CancelScan();
            _scanCts = new CancellationTokenSource();
            CommandManager.InvalidateRequerySuggested();

            PlayerStatus = "Scanning...";

            try
            {
                Cameras.Clear();

                const int start = 200;
                const int end = 299;
                int total = end - start + 1;
                int onlineCount = 0;

                // 동시성 제한
                var limiter = new SemaphoreSlim(16);

                var tasks = Enumerable.Range(start, total).Select(async i =>
                {
                    await limiter.WaitAsync(_scanCts.Token);
                    try
                    {
                        string ip = "192.168.10." + i;
                        bool online = await IsHostUp(ip, _scanCts.Token);

                        if (online)
                        {
                            var item = new CameraItem
                            {
                                Ip = ip,
                                IsOnline = true,
                                // StreamUrl = $"http://{ip}/mjpeg" // 필요 시 규칙에 맞게 설정
                            };

                            await Application.Current.Dispatcher.InvokeAsync(new Action(() =>
                            {
                                // 온라인만 추가 -> 뷰에는 바로 표시
                                Cameras.Add(item);
                            }));

                            Interlocked.Increment(ref onlineCount);
                            PlayerStatus = string.Format("Scanning... {0}/{1} online", onlineCount, total);
                        }
                    }
                    finally
                    {
                        limiter.Release();
                    }
                }).ToArray();

                await Task.WhenAll(tasks);

                PlayerStatus = string.Format("Scan done. {0}/{1} online", onlineCount, total);
            }
            catch (OperationCanceledException)
            {
                PlayerStatus = "Scan canceled.";
            }
            catch (Exception ex)
            {
                PlayerStatus = "Scan error: " + ex.Message;
            }
            finally
            {
                _scanCts = null;
                RefreshViewAndCommands();
            }
        }

        private void CancelScan()
        {
            if (_scanCts != null && !_scanCts.IsCancellationRequested)
                _scanCts.Cancel();
        }

        #endregion

        #region Browser Open Logic

        private void OpenSelectedInBrowser()
        {
            var targets = Cameras.Where(c => c.IsOnline && c.IsSelected).ToList();
            if (targets.Count == 0)
            {
                PlayerStatus = "No camera selected.";
                return;
            }

            bool hasCred = !string.IsNullOrWhiteSpace(Username); // 비번은 빈 문자열이어도 됨

            foreach (var cam in targets)
            {
                try
                {
                    if (hasCred)
                    {
                        // 자격증명 포함 URL만 연다 (일반 URL은 열지 않음)
                        string u = Uri.EscapeDataString(Username ?? "");
                        string p = Uri.EscapeDataString(Password ?? "");
                        string authUrl = string.Format("http://{0}:{1}@{2}/", u, p, cam.Ip);
                        Process.Start(authUrl);
                    }
                    else
                    {
                        // 자격증명 미입력 시 일반 URL만 연다
                        string url = "http://" + cam.Ip + "/";
                        Process.Start(url);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Open browser failed: " + ex.Message);

                    // (옵션) 브라우저가 자격증명 URL을 막는 경우, 일반 URL로만 폴백
                    if (hasCred)
                    {
                        try { Process.Start("http://" + cam.Ip + "/"); } catch { /* ignore */ }
                    }
                }
            }

            PlayerStatus = hasCred
                ? "Opened selected cameras with credentials."
                : "Opened selected cameras.";
        }

        #endregion

        #region Network Helpers (Ping / Port Check)

        private async Task<bool> IsHostUp(string ip, CancellationToken ct)
        {
            try
            {
                using (var p = new Ping())
                {
                    var reply = await p.SendPingAsync(ip, 400);
                    if (reply.Status == IPStatus.Success) return true;
                }
            }
            catch
            {
                // ignore
            }

            // Ping 차단 환경 대비: 80/554 포트 간단 체크
            if (await IsPortOpen(ip, 80, 400, ct)) return true;
            if (await IsPortOpen(ip, 554, 400, ct)) return true;

            return false;
        }

        private async Task<bool> IsPortOpen(string host, int port, int timeoutMs, CancellationToken ct)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    var ar = client.BeginConnect(host, port, null, null);
                    var wh = ar.AsyncWaitHandle;

                    try
                    {
                        bool ok = await Task.Run(() => wh.WaitOne(timeoutMs), ct);
                        if (ok)
                        {
                            client.EndConnect(ar);
                            return true;
                        }

                        return false;
                    }
                    finally
                    {
                        wh.Close();
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Collection / Commands Refresh

        private void Cameras_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (var obj in e.NewItems)
                {
                    var cam = obj as CameraItem;
                    if (cam != null)
                        cam.PropertyChanged += CameraItem_PropertyChanged;
                }
            }

            if (e.OldItems != null)
            {
                foreach (var obj in e.OldItems)
                {
                    var cam = obj as CameraItem;
                    if (cam != null)
                        cam.PropertyChanged -= CameraItem_PropertyChanged;
                }
            }

            RefreshViewAndCommands();
        }

        private void CameraItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsOnline" || e.PropertyName == "IsSelected")
            {
                RefreshViewAndCommands();
            }
        }

        private void RefreshViewAndCommands()
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (CamerasView != null)
                    CamerasView.Refresh();

                CommandManager.InvalidateRequerySuggested();
            }));
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string p = null)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(p));
        }

        #endregion
    }

    #endregion
}
