using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.IO;
using System.Windows.Input;
using Microsoft.Win32;
using STK_ToolBox.Helpers; // RelayCommand

namespace STK_ToolBox.ViewModels
{
    /// <summary>
    /// ServoParameter.ini용 파라미터 편집 페이지 VM
    /// - INI 경로/축 개수/선택 축 관리
    /// - Limit / Origin 서브 ViewModel 동기화
    /// - INI 백업/백업 폴더 열기 기능
    /// </summary>
    public class ParameterPageViewModel : INotifyPropertyChanged
    {
        #region ──────────────── 필드 (Fields) ────────────────

        private bool _syncing;

        private string _iniPath = @"D:\LBS_DB\ServoParameter.ini";
        private int _axisCount = 4;
        private string _selectedAxis = "AXIS_1";

        private string _backupDirectory;
        private int _maxBackupFiles = 20; // 0 = 무제한

        #endregion


        #region ──────────────── INI / 축 설정 바인딩 (Ini / Axis Bindings) ────────────────

        /// <summary>ServoParameter.ini 파일 경로</summary>
        public string IniPath
        {
            get => _iniPath;
            set
            {
                if (_iniPath == value) return;
                _iniPath = value;
                OnPropertyChanged();

                // 자식 VM에 경로 전달
                LimitVM.IniPath = value;
                OriginVM.IniPath = value;
            }
        }

        /// <summary>축 개수 (AXIS_1 ~ AXIS_n)</summary>
        public int AxisCount
        {
            get => _axisCount;
            set
            {
                if (_axisCount == value) return;
                _axisCount = value;
                OnPropertyChanged();

                BuildAxisList(value);

                // 자식 VM에 축 개수 전달
                LimitVM.AxisCount = value;
                OriginVM.AxisCount = value;
            }
        }

        /// <summary>현재 선택된 축 이름 (예: AXIS_1)</summary>
        public string SelectedAxis
        {
            get => _selectedAxis;
            set
            {
                if (_selectedAxis == value) return;
                _selectedAxis = value;
                OnPropertyChanged();

                // 자식 VM에 선택 축 전달
                LimitVM.SelectedAxis = value;
                OriginVM.SelectedAxis = value;
            }
        }

        /// <summary>콤보박스에 바인딩되는 축 목록</summary>
        public ObservableCollection<string> AxisList { get; } = new ObservableCollection<string>();

        #endregion


        #region ──────────────── 서브 ViewModel (Child ViewModels) ────────────────

        public LimitCalculatorViewModel LimitVM { get; } = new LimitCalculatorViewModel();
        public OriginAxisViewModel OriginVM { get; } = new OriginAxisViewModel();

        #endregion


        #region ──────────────── 백업 설정 (Backup Settings) ────────────────

        /// <summary>백업 폴더 (미지정 시 INI 폴더 하위 Backup 사용)</summary>
        public string BackupDirectory
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_backupDirectory))
                {
                    try
                    {
                        var baseDir = string.IsNullOrWhiteSpace(IniPath) ? null : Path.GetDirectoryName(IniPath);
                        if (!string.IsNullOrWhiteSpace(baseDir))
                            return Path.Combine(baseDir, "Backup");
                    }
                    catch { }
                }
                return _backupDirectory;
            }
            set
            {
                _backupDirectory = value;
                OnPropertyChanged();
            }
        }

        /// <summary>축적할 최대 백업 개수 (0 = 무제한)</summary>
        public int MaxBackupFiles
        {
            get => _maxBackupFiles;
            set
            {
                _maxBackupFiles = Math.Max(0, value);
                OnPropertyChanged();
            }
        }

        #endregion


        #region ──────────────── 커맨드 (Commands) ────────────────

        public ICommand BrowseCommand { get; }
        public ICommand BackupCommand { get; }
        public ICommand OpenBackupFolderCommand { get; }

        #endregion


        #region ──────────────── 생성자 (Constructor) ────────────────

        public ParameterPageViewModel()
        {
            // 축 리스트 초기화
            BuildAxisList(AxisCount);

            // 자식 VM 초기 설정
            LimitVM.IniPath = IniPath;
            LimitVM.AxisCount = AxisCount;
            LimitVM.SelectedAxis = SelectedAxis;

            OriginVM.IniPath = IniPath;
            OriginVM.AxisCount = AxisCount;
            OriginVM.SelectedAxis = SelectedAxis;

            // 자식 VM 변경사항 동기화
            LimitVM.PropertyChanged += ChildVM_PropertyChanged;
            OriginVM.PropertyChanged += ChildVM_PropertyChanged;

            // 커맨드 초기화
            BrowseCommand = new RelayCommand(() => OpenBrowseDialog());
            BackupCommand = new RelayCommand(() => BackupIniFile());
            OpenBackupFolderCommand = new RelayCommand(() => OpenBackupFolder());
        }

        #endregion


        #region ──────────────── INI 파일 선택 다이얼로그 (Browse) ────────────────

        private void OpenBrowseDialog()
        {
            var dlg = new OpenFileDialog
            {
                Filter = "INI 파일 (*.ini)|*.ini|모든 파일 (*.*)|*.*",
                InitialDirectory = GuessInitialDirectory()
            };

            if (dlg.ShowDialog() == true)
            {
                IniPath = dlg.FileName;
            }
        }

        private string GuessInitialDirectory()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(IniPath))
                {
                    var dir = Path.GetDirectoryName(IniPath);
                    if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                        return dir;
                }
            }
            catch { }

            return @"D:\";
        }

        #endregion


        #region ──────────────── 축 목록 구성 (AxisList) ────────────────

        private void BuildAxisList(int count)
        {
            AxisList.Clear();
            for (int i = 1; i <= count; i++)
                AxisList.Add("AXIS_" + i);

            // 현재 선택 축이 리스트에 없으면 첫 번째 축으로 보정
            if (AxisList.Count > 0 && !AxisList.Contains(SelectedAxis))
                SelectedAxis = AxisList[0];
        }

        #endregion


        #region ──────────────── 자식 VM 변경 동기화 (Child VM Sync) ────────────────

        private void ChildVM_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_syncing) return;
            _syncing = true;

            try
            {
                var lvm = sender as LimitCalculatorViewModel;
                var ovm = sender as OriginAxisViewModel;

                if (lvm != null)
                {
                    switch (e.PropertyName)
                    {
                        case nameof(LimitCalculatorViewModel.IniPath):
                            if (IniPath != lvm.IniPath) IniPath = lvm.IniPath;
                            break;

                        case nameof(LimitCalculatorViewModel.AxisCount):
                            if (AxisCount != lvm.AxisCount) AxisCount = lvm.AxisCount;
                            break;

                        case nameof(LimitCalculatorViewModel.SelectedAxis):
                            if (SelectedAxis != lvm.SelectedAxis) SelectedAxis = lvm.SelectedAxis;
                            break;
                    }
                }
                else if (ovm != null)
                {
                    switch (e.PropertyName)
                    {
                        case nameof(OriginAxisViewModel.IniPath):
                            if (IniPath != ovm.IniPath) IniPath = ovm.IniPath;
                            break;

                        case nameof(OriginAxisViewModel.AxisCount):
                            if (AxisCount != ovm.AxisCount) AxisCount = ovm.AxisCount;
                            break;

                        case nameof(OriginAxisViewModel.SelectedAxis):
                            if (SelectedAxis != ovm.SelectedAxis) SelectedAxis = ovm.SelectedAxis;
                            break;
                    }
                }
            }
            finally
            {
                _syncing = false;
            }
        }

        #endregion


        #region ──────────────── INI 백업 (Backup) ────────────────

        private void BackupIniFile()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(IniPath) || !File.Exists(IniPath))
                {
                    System.Windows.MessageBox.Show("유효한 INI 파일이 없습니다.");
                    return;
                }

                var backupDir = BackupDirectory;
                if (string.IsNullOrWhiteSpace(backupDir))
                {
                    System.Windows.MessageBox.Show("백업 폴더를 결정할 수 없습니다.");
                    return;
                }

                Directory.CreateDirectory(backupDir);

                var srcNameNoExt = Path.GetFileNameWithoutExtension(IniPath);
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var dstName = string.Format("{0}_{1}.ini", srcNameNoExt, timestamp);
                var dstFull = Path.Combine(backupDir, dstName);

                int seq = 1;
                while (File.Exists(dstFull))
                {
                    dstName = string.Format("{0}_{1}_{2}.ini", srcNameNoExt, timestamp, seq);
                    dstFull = Path.Combine(backupDir, dstName);
                    seq++;
                }

                File.Copy(IniPath, dstFull, false);

                if (MaxBackupFiles > 0)
                    EnforceBackupRetention(backupDir, srcNameNoExt, MaxBackupFiles);

                System.Windows.MessageBox.Show("백업 완료:\n" + dstFull);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("백업 오류: " + ex.Message);
            }
        }

        private void EnforceBackupRetention(string backupDir, string srcNameNoExt, int maxKeep)
        {
            try
            {
                var patternPrefix = srcNameNoExt + "_";

                var files = new DirectoryInfo(backupDir)
                    .GetFiles("*.ini", SearchOption.TopDirectoryOnly)
                    .Where(f => f.Name.StartsWith(patternPrefix, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(f => f.CreationTimeUtc)
                    .ToList();

                if (files.Count <= maxKeep) return;

                foreach (var f in files.Skip(maxKeep))
                {
                    try { f.Delete(); } catch { }
                }
            }
            catch { }
        }

        private void OpenBackupFolder()
        {
            try
            {
                var backupDir = BackupDirectory;
                if (string.IsNullOrWhiteSpace(backupDir))
                {
                    System.Windows.MessageBox.Show("백업 폴더를 결정할 수 없습니다.");
                    return;
                }

                Directory.CreateDirectory(backupDir);

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = backupDir,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("폴더 열기 오류: " + ex.Message);
            }
        }

        #endregion


        #region ──────────────── INotifyPropertyChanged ────────────────

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string name = "")
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        #endregion
    }
}
