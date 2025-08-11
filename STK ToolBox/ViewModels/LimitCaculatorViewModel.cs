using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace STK_ToolBox.ViewModels
{
    public class LimitCalculatorViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<string> AxisList { get; set; } = new ObservableCollection<string>();

        private string _iniPath = @"D:\LBS_DB\ServoParameter.ini";
        public string IniPath
        {
            get => _iniPath;
            set { _iniPath = value; OnPropertyChanged(); ReadAndDisplayAxisLimits(); }
        }

        private string _selectedAxis;
        public string SelectedAxis
        {
            get => _selectedAxis;
            set { _selectedAxis = value; OnPropertyChanged(); ReadAndDisplayAxisLimits(); }
        }

        private int _axisCount = 4;
        public int AxisCount
        {
            get => _axisCount;
            set { _axisCount = value; OnPropertyChanged(); UpdateAxisList(value); }
        }

        public string SoftLimitPlus { get; set; }
        public string SoftLimitPlusDelta { get; set; }
        public string SoftLimitMinus { get; set; }
        public string SoftLimitMinusDelta { get; set; }

        private int _softPlus;
        private int _softMinus;

        public string CurrentSoftLimitPlusDecimal => _softPlus.ToString();
        public string CurrentSoftLimitPlusHighHex => ((_softPlus >> 16) & 0xFFFF).ToString("X4");
        public string CurrentSoftLimitPlusLowHex => (_softPlus & 0xFFFF).ToString("X4");

        public string CurrentSoftLimitMinusDecimal => _softMinus.ToString();
        public string CurrentSoftLimitMinusHighHex => ((_softMinus >> 16) & 0xFFFF).ToString("X4");
        public string CurrentSoftLimitMinusLowHex => (_softMinus & 0xFFFF).ToString("X4");

        public ICommand ApplyCommand { get; }
        public ICommand ZeroParamsCommand { get; }
        public ICommand BrowseCommand { get; }

        public LimitCalculatorViewModel()
        {
            UpdateAxisList(AxisCount);

            ApplyCommand = new RelayCommand(ApplyLimits);
            ZeroParamsCommand = new RelayCommand(ZeroParams);
            BrowseCommand = new RelayCommand(OpenBrowseDialog);

            ReadAndDisplayAxisLimits();
        }

        private void UpdateAxisList(int count)
        {
            AxisList.Clear();
            for (int i = 1; i <= count; i++)
                AxisList.Add($"AXIS_{i}");
            if (AxisList.Count > 0)
                SelectedAxis = AxisList[0];
        }

        private void ReadAndDisplayAxisLimits()
        {
            _softPlus = 0;
            _softMinus = 0;

            if (!File.Exists(IniPath) || string.IsNullOrEmpty(SelectedAxis))
            {
                NotifySoftLimitChange();
                return;
            }

            string[] lines = File.ReadAllLines(IniPath);
            int start = Array.FindIndex(lines, l => l.Trim().Equals($"[{SelectedAxis}]", StringComparison.OrdinalIgnoreCase));
            if (start == -1) return;

            int end = Array.FindIndex(lines, start + 1, l => l.TrimStart().StartsWith("[AXIS_", StringComparison.OrdinalIgnoreCase));
            if (end == -1) end = lines.Length;

            int? p228 = null, p229 = null, p22A = null, p22B = null;

            for (int i = start; i < end; i++)
            {
                string line = lines[i].Trim();
                var parts = line.Split('=');
                if (parts.Length != 2) continue;

                string key = parts[0].Trim();
                string val = parts[1].Trim();

                if (int.TryParse(val, System.Globalization.NumberStyles.HexNumber, null, out int parsed))
                {
                    switch (key.ToUpper())
                    {
                        case "PR.228": p228 = parsed; break;
                        case "PR.229": p229 = parsed; break;
                        case "PR.22A": p22A = parsed; break;
                        case "PR.22B": p22B = parsed; break;
                    }
                }
            }

            _softPlus = ((p229 ?? 0) << 16) | (p228 ?? 0);
            _softMinus = ((p22B ?? 0) << 16) | (p22A ?? 0);

            NotifySoftLimitChange();
        }

        private void NotifySoftLimitChange()
        {
            OnPropertyChanged(nameof(CurrentSoftLimitPlusDecimal));
            OnPropertyChanged(nameof(CurrentSoftLimitPlusHighHex));
            OnPropertyChanged(nameof(CurrentSoftLimitPlusLowHex));
            OnPropertyChanged(nameof(CurrentSoftLimitMinusDecimal));
            OnPropertyChanged(nameof(CurrentSoftLimitMinusHighHex));
            OnPropertyChanged(nameof(CurrentSoftLimitMinusLowHex));
        }

        private void ApplyLimits()
        {
            if (!int.TryParse(SoftLimitPlus, out int plus) ||
                !int.TryParse(SoftLimitPlusDelta, out int deltaPlus) ||
                !int.TryParse(SoftLimitMinus, out int minus) ||
                !int.TryParse(SoftLimitMinusDelta, out int deltaMinus))
            {
                MessageBox.Show("정수 값을 입력하세요.");
                return;
            }

            int finalPlus = plus + deltaPlus;
            int finalMinus = minus - deltaMinus;

            UpdateParamsInFile(finalPlus, finalMinus);
        }

        private void ZeroParams()
        {
            UpdateParamsInFile(0, 0);
        }

        private void UpdateParamsInFile(int plus, int minus)
        {
            if (!File.Exists(IniPath) || string.IsNullOrEmpty(SelectedAxis))
            {
                MessageBox.Show("INI 파일 또는 AXIS 정보가 유효하지 않습니다.");
                return;
            }

            string[] lines = File.ReadAllLines(IniPath);
            int start = Array.FindIndex(lines, l => l.Trim().Equals($"[{SelectedAxis}]"));
            if (start == -1) return;

            int end = Array.FindIndex(lines, start + 1, l => l.TrimStart().StartsWith("[AXIS_"));
            if (end == -1) end = lines.Length;

            var map = new (string key, int value)[]
            {
                ("Pr.228", plus & 0xFFFF),
                ("Pr.229", (plus >> 16) & 0xFFFF),
                ("Pr.22A", minus & 0xFFFF),
                ("Pr.22B", (minus >> 16) & 0xFFFF)
            };

            for (int i = start; i < end; i++)
            {
                foreach (var (key, val) in map)
                {
                    if (lines[i].TrimStart().StartsWith(key))
                        lines[i] = $"{key} = {val:X4}";
                }
            }

            File.WriteAllLines(IniPath, lines);
            ReadAndDisplayAxisLimits();
            MessageBox.Show("INI 파일 업데이트 완료");
        }

        private void OpenBrowseDialog()
        {
            OpenFileDialog dlg = new OpenFileDialog
            {
                Filter = "INI 파일 (*.ini)|*.ini",
                InitialDirectory = @"D:\"
            };
            if (dlg.ShowDialog() == true)
                IniPath = dlg.FileName;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}