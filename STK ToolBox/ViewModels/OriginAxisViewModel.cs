using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace STK_ToolBox.ViewModels
{
    public class OriginAxisViewModel : INotifyPropertyChanged
    {
        private string _iniPath = @"D:\LBS_DB\ServoParameter.ini";
        public string IniPath
        {
            get => _iniPath;
            set
            {
                _iniPath = value;
                OnPropertyChanged();
                if (!_suppressRead) ReadAndDisplayOriginAxis();
            }
        }

        private string _selectedAxis;
        public string SelectedAxis
        {
            get => _selectedAxis;
            set
            {
                _selectedAxis = value;
                OnPropertyChanged();
                if (!_suppressRead) ReadAndDisplayOriginAxis();
            }
        }

        private int _axisCount = 4;
        public int AxisCount
        {
            get => _axisCount;
            set { _axisCount = value; OnPropertyChanged(); }
        }

        // 입력값(변경 시 프리뷰 갱신)
        private string _changeAxis;
        public string ChangeAxis
        {
            get => _changeAxis;
            set
            {
                _changeAxis = value;
                OnPropertyChanged();
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                    _previewAxis = v;
                else
                    _previewAxis = _originAxis; // 잘못 입력 시 현재값
                NotifyPreview();
            }
        }

        // INI에서 읽은 현재값
        private int _originAxis;
        public string CurrentOriginAxisDecimal => _originAxis.ToString();
        public string CurrentOriginAxisHighHex => ((_originAxis >> 16) & 0xFFFF).ToString("X4");
        public string CurrentOriginAxisLowHex => (_originAxis & 0xFFFF).ToString("X4");

        // 미리보기 값
        private int _previewAxis;
        public string PreviewOriginAxisDecimal => _previewAxis.ToString();
        public string PreviewOriginAxisHighHex => ((_previewAxis >> 16) & 0xFFFF).ToString("X4");
        public string PreviewOriginAxisLowHex => (_previewAxis & 0xFFFF).ToString("X4");

        public ICommand ApplyCommand { get; }
        public ICommand ZeroParamsCommand { get; }

        //private bool _isBusy;
        private bool _suppressRead;

        public OriginAxisViewModel()
        {
            ApplyCommand = new RelayCommand(() => ApplyLimits());
            ZeroParamsCommand = new RelayCommand(() => ZeroParams());

            ReadAndDisplayOriginAxis();
        }

        private void ReadAndDisplayOriginAxis()
        {
   /*         if (_isBusy) return;
            _isBusy = true;*/
            try
            {
                _originAxis = 0;

                if (string.IsNullOrWhiteSpace(IniPath) || !File.Exists(IniPath) || string.IsNullOrWhiteSpace(SelectedAxis))
                {
                    NotifyAxisChange();
                    _previewAxis = _originAxis;
                    NotifyPreview();
                    return;
                }

                var lines = File.ReadAllLines(IniPath);
                int start = Array.FindIndex(lines, l => l.Trim().Equals($"[{SelectedAxis}]", StringComparison.OrdinalIgnoreCase));
                if (start == -1)
                {
                    NotifyAxisChange();
                    _previewAxis = _originAxis;
                    NotifyPreview();
                    return;
                }

                int end = Array.FindIndex(lines, start + 1, l => l.TrimStart().StartsWith("[AXIS_", StringComparison.OrdinalIgnoreCase));
                if (end == -1) end = lines.Length;

                int? p246 = null, p247 = null;

                for (int i = start; i < end; i++)
                {
                    string line = lines[i].Trim();
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;

                    string key = line.Substring(0, eq).Trim();
                    string val = line.Substring(eq + 1).Trim();

                    if (int.TryParse(val, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int parsed))
                    {
                        switch (key.ToUpperInvariant())
                        {
                            case "PR.246": p246 = parsed; break;
                            case "PR.247": p247 = parsed; break;
                        }
                    }
                }

                _originAxis = ((p247 ?? 0) << 16) | (p246 ?? 0);
                _previewAxis = _originAxis; // 읽은 직후 프리뷰 초기화

                NotifyAxisChange();
                NotifyPreview();
            }
            catch (Exception ex)
            {
                MessageBox.Show("INI 읽기 오류: " + ex.Message);
            }
            finally
            {
                //_isBusy = false;
            }
        }

        private void NotifyAxisChange()
        {
            OnPropertyChanged(nameof(CurrentOriginAxisDecimal));
            OnPropertyChanged(nameof(CurrentOriginAxisHighHex));
            OnPropertyChanged(nameof(CurrentOriginAxisLowHex));
        }

        private void NotifyPreview()
        {
            OnPropertyChanged(nameof(PreviewOriginAxisDecimal));
            OnPropertyChanged(nameof(PreviewOriginAxisHighHex));
            OnPropertyChanged(nameof(PreviewOriginAxisLowHex));
        }

        private void ApplyLimits()
        {
            //if (_isBusy) return;

            if (!int.TryParse(ChangeAxis, out int axis))
            {
                MessageBox.Show("정수 값을 입력하세요.");
                return;
            }

            UpdateParamsInFile(axis);
        }

        private void ZeroParams()
        {
            //if (_isBusy) return;
            UpdateParamsInFile(0);
        }

        private void UpdateParamsInFile(int axis)
        {
/*            if (_isBusy) return;
            _isBusy = true;*/
            try
            {
                if (string.IsNullOrWhiteSpace(IniPath) || !File.Exists(IniPath) || string.IsNullOrWhiteSpace(SelectedAxis))
                {
                    MessageBox.Show("INI 파일 또는 AXIS 정보가 유효하지 않습니다.");
                    return;
                }

                var lines = File.ReadAllLines(IniPath);
                int start = Array.FindIndex(lines, l => l.Trim().Equals($"[{SelectedAxis}]", StringComparison.OrdinalIgnoreCase));
                if (start == -1) return;

                int end = Array.FindIndex(lines, start + 1, l => l.TrimStart().StartsWith("[AXIS_", StringComparison.OrdinalIgnoreCase));
                if (end == -1) end = lines.Length;

                int low = axis & 0xFFFF;
                int high = (axis >> 16) & 0xFFFF;

                bool has246 = false, has247 = false;

                for (int i = start; i < end; i++)
                {
                    string raw = lines[i];
                    string trimmed = raw.TrimStart();
                    int eq = trimmed.IndexOf('=');
                    if (eq <= 0) continue;

                    string key = trimmed.Substring(0, eq).Trim().ToUpperInvariant();

                    if (key == "PR.246")
                    {
                        lines[i] = ReplaceKeepingIndent(raw, $"Pr.246 = {low:X4}");
                        has246 = true;
                    }
                    else if (key == "PR.247")
                    {
                        lines[i] = ReplaceKeepingIndent(raw, $"Pr.247 = {high:X4}");
                        has247 = true;
                    }
                }

                if (!has246 || !has247)
                {
                    var list = new System.Collections.Generic.List<string>(lines);
                    int insertAt = end;
                    if (!has246) list.Insert(insertAt++, $"Pr.246 = {low:X4}");
                    if (!has247) list.Insert(insertAt++, $"Pr.247 = {high:X4}");
                    lines = list.ToArray();
                }

                File.WriteAllLines(IniPath, lines);

                _suppressRead = true;
                ReadAndDisplayOriginAxis();
                _suppressRead = false;

                MessageBox.Show("INI 파일 업데이트 완료");
            }
            catch (Exception ex)
            {
                MessageBox.Show("INI 쓰기 오류: " + ex.Message);
            }
            finally
            {
                //_isBusy = false;
            }
        }

        private static string ReplaceKeepingIndent(string originalLine, string newContent)
        {
            int idx = 0;
            while (idx < originalLine.Length && char.IsWhiteSpace(originalLine[idx])) idx++;
            string indent = originalLine.Substring(0, idx);
            return indent + newContent;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
