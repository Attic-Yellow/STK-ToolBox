using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.IO;
using System.Windows.Input;
using Microsoft.Win32;

namespace STK_ToolBox.ViewModels
{
    public class ParameterPageViewModel : INotifyPropertyChanged
    {
        private bool _syncing; // ← 순환 업데이트 방지

        private string _iniPath = @"D:\LBS_DB\ServoParameter.ini";
        public string IniPath
        {
            get => _iniPath;
            set
            {
                if (_iniPath == value) return;
                _iniPath = value;
                OnPropertyChanged();
                // 자식 VM에 전파
                LimitVM.IniPath = value;
                OriginVM.IniPath = value;
            }
        }

        private int _axisCount = 4;
        public int AxisCount
        {
            get => _axisCount;
            set
            {
                if (_axisCount == value) return;
                _axisCount = value;
                OnPropertyChanged();
                BuildAxisList(value);
                // 자식 VM에 전파
                LimitVM.AxisCount = value;
                OriginVM.AxisCount = value;
            }
        }

        private string _selectedAxis = "AXIS_1";
        public string SelectedAxis
        {
            get => _selectedAxis;
            set
            {
                if (_selectedAxis == value) return;
                _selectedAxis = value;
                OnPropertyChanged();
                // 자식 VM에 전파
                LimitVM.SelectedAxis = value;
                OriginVM.SelectedAxis = value;
            }
        }

        public ObservableCollection<string> AxisList { get; } = new ObservableCollection<string>();

        public LimitCalculatorViewModel LimitVM { get; } = new LimitCalculatorViewModel();
        public OriginAxisViewModel OriginVM { get; } = new OriginAxisViewModel();

        // ⬇⬇ 추가: 찾아보기 커맨드
        public ICommand BrowseCommand { get; }

        public ParameterPageViewModel()
        {
            BuildAxisList(AxisCount);

            // 초기 전파 (부모 → 자식)
            LimitVM.IniPath = IniPath;
            LimitVM.AxisCount = AxisCount;
            LimitVM.SelectedAxis = SelectedAxis;

            OriginVM.IniPath = IniPath;
            OriginVM.AxisCount = AxisCount;
            OriginVM.SelectedAxis = SelectedAxis;

            // ⬇⬇ 자식 변화 구독 (자식 → 부모)
            LimitVM.PropertyChanged += ChildVM_PropertyChanged;
            OriginVM.PropertyChanged += ChildVM_PropertyChanged;

            BrowseCommand = new RelayCommand(() => OpenBrowseDialog());
        }

        private void OpenBrowseDialog()
        {
            var dlg = new OpenFileDialog
            {
                Filter = "INI 파일 (*.ini)|*.ini|모든 파일 (*.*)|*.*",
                InitialDirectory = GuessInitialDirectory()
            };
            if (dlg.ShowDialog() == true)
            {
                IniPath = dlg.FileName; // setter에서 자식 VM들로 전파됨
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

        private void BuildAxisList(int count)
        {
            AxisList.Clear();
            for (int i = 1; i <= count; i++)
                AxisList.Add($"AXIS_{i}");
            if (AxisList.Count > 0 && !AxisList.Contains(SelectedAxis))
                SelectedAxis = AxisList[0];
        }

        private void ChildVM_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_syncing) return;
            _syncing = true;
            try
            {
                if (sender is LimitCalculatorViewModel lvm)
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
                else if (sender is OriginAxisViewModel ovm)
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

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
