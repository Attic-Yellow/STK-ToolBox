using Microsoft.Win32; // BrowseCommand 제거했으면 없어도 됨
using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace STK_ToolBox.ViewModels
{
    public class LimitCalculatorViewModel : INotifyPropertyChanged
    {
        // 부모가 주입해주는 값들 (상단 고정 패널에서 설정)
        private string _iniPath = @"D:\LBS_DB\ServoParameter.ini";
        public string IniPath
        {
            get => _iniPath;
            set
            {
                _iniPath = value;
                OnPropertyChanged();
                if (!_suppressRead) ReadAndDisplayAxisLimits();
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
                if (!_suppressRead) ReadAndDisplayAxisLimits();
            }
        }

        // 유지용
        private int _axisCount = 4;
        public int AxisCount
        {
            get => _axisCount;
            set { _axisCount = value; OnPropertyChanged(); }
        }

        // ===== 입력값 (setter에서 프리뷰 재계산) =====
        private string _softLimitPlus, _softLimitPlusDelta, _softLimitMinus, _softLimitMinusDelta;
        public string SoftLimitPlus { get => _softLimitPlus; set { _softLimitPlus = value; OnPropertyChanged(); RecalcPreview(); } }
        public string SoftLimitPlusDelta { get => _softLimitPlusDelta; set { _softLimitPlusDelta = value; OnPropertyChanged(); RecalcPreview(); } }
        public string SoftLimitMinus { get => _softLimitMinus; set { _softLimitMinus = value; OnPropertyChanged(); RecalcPreview(); } }
        public string SoftLimitMinusDelta { get => _softLimitMinusDelta; set { _softLimitMinusDelta = value; OnPropertyChanged(); RecalcPreview(); } }

        // ===== INI에서 읽은 현재값(고정 표시) =====
        private int _softPlus;
        private int _softMinus;

        public string CurrentSoftLimitPlusDecimal => _softPlus.ToString();
        public string CurrentSoftLimitPlusHighHex => ((_softPlus >> 16) & 0xFFFF).ToString("X4");
        public string CurrentSoftLimitPlusLowHex => (_softPlus & 0xFFFF).ToString("X4");

        public string CurrentSoftLimitMinusDecimal => _softMinus.ToString();
        public string CurrentSoftLimitMinusHighHex => ((_softMinus >> 16) & 0xFFFF).ToString("X4");
        public string CurrentSoftLimitMinusLowHex => (_softMinus & 0xFFFF).ToString("X4");

        // ===== 미리보기 값(입력에 따라 즉시 반영) =====
        private int _previewPlus, _previewMinus;

        public string PreviewSoftLimitPlusDecimal => _previewPlus.ToString();
        public string PreviewSoftLimitPlusHighHex => ((_previewPlus >> 16) & 0xFFFF).ToString("X4");
        public string PreviewSoftLimitPlusLowHex => (_previewPlus & 0xFFFF).ToString("X4");

        public string PreviewSoftLimitMinusDecimal => _previewMinus.ToString();
        public string PreviewSoftLimitMinusHighHex => ((_previewMinus >> 16) & 0xFFFF).ToString("X4");
        public string PreviewSoftLimitMinusLowHex => (_previewMinus & 0xFFFF).ToString("X4");

        // 커맨드
        public ICommand ApplyCommand { get; }
        public ICommand ZeroParamsCommand { get; }

        // 가드
        //private bool _isBusy;
        private bool _suppressRead;

        public LimitCalculatorViewModel()
        {
            ApplyCommand = new RelayCommand(() => ApplyLimits());
            ZeroParamsCommand = new RelayCommand(() => ZeroParams());

            ReadAndDisplayAxisLimits(); // 초기 1회 로드
        }

        private void ReadAndDisplayAxisLimits()
        {
/*            if (_isBusy) return;
            _isBusy = true;*/
            try
            {
                _softPlus = 0;
                _softMinus = 0;

                if (string.IsNullOrWhiteSpace(IniPath) || !File.Exists(IniPath) || string.IsNullOrWhiteSpace(SelectedAxis))
                {
                    NotifySoftLimitChange();
                    RecalcPreview();
                    return;
                }

                var lines = File.ReadAllLines(IniPath);
                int start = Array.FindIndex(lines, l => l.Trim().Equals($"[{SelectedAxis}]", StringComparison.OrdinalIgnoreCase));
                if (start == -1)
                {
                    NotifySoftLimitChange();
                    RecalcPreview();
                    return;
                }

                int end = Array.FindIndex(lines, start + 1, l => l.TrimStart().StartsWith("[AXIS_", StringComparison.OrdinalIgnoreCase));
                if (end == -1) end = lines.Length;

                int? p228 = null, p229 = null, p22A = null, p22B = null;

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
                RecalcPreview(); // 현재값 기준으로 프리뷰도 초기화
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

        private void NotifySoftLimitChange()
        {
            OnPropertyChanged(nameof(CurrentSoftLimitPlusDecimal));
            OnPropertyChanged(nameof(CurrentSoftLimitPlusHighHex));
            OnPropertyChanged(nameof(CurrentSoftLimitPlusLowHex));
            OnPropertyChanged(nameof(CurrentSoftLimitMinusDecimal));
            OnPropertyChanged(nameof(CurrentSoftLimitMinusHighHex));
            OnPropertyChanged(nameof(CurrentSoftLimitMinusLowHex));
        }

        private void RecalcPreview()
        {
            int plus = TryParseOr(_softLimitPlus, _softPlus);
            int deltaPlus = TryParseOr(_softLimitPlusDelta, 0);
            int minus = TryParseOr(_softLimitMinus, _softMinus);
            int deltaMinus = TryParseOr(_softLimitMinusDelta, 0);

            _previewPlus = plus + deltaPlus;
            _previewMinus = minus - deltaMinus;

            OnPropertyChanged(nameof(PreviewSoftLimitPlusDecimal));
            OnPropertyChanged(nameof(PreviewSoftLimitPlusHighHex));
            OnPropertyChanged(nameof(PreviewSoftLimitPlusLowHex));
            OnPropertyChanged(nameof(PreviewSoftLimitMinusDecimal));
            OnPropertyChanged(nameof(PreviewSoftLimitMinusHighHex));
            OnPropertyChanged(nameof(PreviewSoftLimitMinusLowHex));
        }

        private static int TryParseOr(string s, int fallback)
        {
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                return v;
            return fallback;
        }

        private void ApplyLimits()
        {
            //if (_isBusy) return;

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
            //if (_isBusy) return;
            UpdateParamsInFile(0, 0);
        }

        private void UpdateParamsInFile(int plus, int minus)
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

                int lowPlus = plus & 0xFFFF;
                int highPlus = (plus >> 16) & 0xFFFF;
                int lowMinus = minus & 0xFFFF;
                int highMinus = (minus >> 16) & 0xFFFF;

                bool has228 = false, has229 = false, has22A = false, has22B = false;

                for (int i = start; i < end; i++)
                {
                    string raw = lines[i];
                    string trimmed = raw.TrimStart();
                    int eq = trimmed.IndexOf('=');
                    if (eq <= 0) continue;

                    string key = trimmed.Substring(0, eq).Trim().ToUpperInvariant();

                    if (key == "PR.228")
                    {
                        lines[i] = ReplaceKeepingIndent(raw, $"Pr.228 = {lowPlus:X4}");
                        has228 = true;
                    }
                    else if (key == "PR.229")
                    {
                        lines[i] = ReplaceKeepingIndent(raw, $"Pr.229 = {highPlus:X4}");
                        has229 = true;
                    }
                    else if (key == "PR.22A")
                    {
                        lines[i] = ReplaceKeepingIndent(raw, $"Pr.22A = {lowMinus:X4}");
                        has22A = true;
                    }
                    else if (key == "PR.22B")
                    {
                        lines[i] = ReplaceKeepingIndent(raw, $"Pr.22B = {highMinus:X4}");
                        has22B = true;
                    }
                }

                if (!has228 || !has229 || !has22A || !has22B)
                {
                    var list = new System.Collections.Generic.List<string>(lines);
                    int insertAt = end;

                    if (!has228) list.Insert(insertAt++, $"Pr.228 = {lowPlus:X4}");
                    if (!has229) list.Insert(insertAt++, $"Pr.229 = {highPlus:X4}");
                    if (!has22A) list.Insert(insertAt++, $"Pr.22A = {lowMinus:X4}");
                    if (!has22B) list.Insert(insertAt++, $"Pr.22B = {highMinus:X4}");

                    lines = list.ToArray();
                }

                File.WriteAllLines(IniPath, lines);

                _suppressRead = true;
                ReadAndDisplayAxisLimits();
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
