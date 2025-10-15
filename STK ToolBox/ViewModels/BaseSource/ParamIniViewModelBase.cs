using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;

namespace STK_ToolBox.ViewModels.BaseSource
{
    /// <summary>
    /// ServoParameter.ini 공통 유틸과 바인딩 프로퍼티를 제공하는 베이스
    /// - IniPath, SelectedAxis, AxisCount
    /// - 섹션 경계 탐색([AXIS_xxx] ~ 다음 [AXIS_..])
    /// - PR.x 값 Hex 파싱/쓰기(들여쓰기 유지)
    /// - _suppressRead 가드
    /// 파생 클래스는 ReadFromIniCore(), WriteToIniCore()를 구현
    /// </summary>
    public abstract class ParamIniViewModelBase : INotifyPropertyChanged
    {
        private string _iniPath = @"D:\LBS_DB\ServoParameter.ini";
        public string IniPath
        {
            get => _iniPath;
            set
            {
                if (_iniPath == value) return;
                _iniPath = value;
                OnPropertyChanged();
                if (!_suppressRead) ReadFromIni();
            }
        }

        private string _selectedAxis;
        public string SelectedAxis
        {
            get => _selectedAxis;
            set
            {
                if (_selectedAxis == value) return;
                _selectedAxis = value;
                OnPropertyChanged();
                if (!_suppressRead) ReadFromIni();
            }
        }

        private int _axisCount = 4;
        public int AxisCount
        {
            get => _axisCount;
            set { if (_axisCount != value) { _axisCount = value; OnPropertyChanged(); } }
        }

        protected bool _suppressRead;

        /// <summary>파생 클래스가 INI 읽기 처리(상태 갱신/Notify 포함)</summary>
        protected abstract void ReadFromIniCore(string[] lines, int start, int end);

        /// <summary>파생 클래스가 INI 쓰기 처리(해당 섹션 범위에서 PR.x 갱신)</summary>
        protected abstract void WriteToIniCore(List<string> lines, int start, ref int end);

        public void ReadFromIni()
        {
            try
            {
                if (!ValidatePathAndAxis(out var lines, out int start, out int end))
                {
                    // 파생 클래스가 "초기 상태로 표시"하도록 빈 섹션 전달
                    ReadFromIniCore(Array.Empty<string>(), 0, 0);
                    return;
                }
                ReadFromIniCore(lines, start, end);
            }
            catch (Exception ex)
            {
                MessageBox.Show("INI 읽기 오류: " + ex.Message);
            }
        }

        protected bool ValidatePathAndAxis(out string[] lines, out int start, out int end)
        {
            lines = null; start = end = -1;

            if (string.IsNullOrWhiteSpace(IniPath) || !File.Exists(IniPath) || string.IsNullOrWhiteSpace(SelectedAxis))
                return false;

            lines = File.ReadAllLines(IniPath);
            start = Array.FindIndex(lines, l => l.Trim().Equals($"[{SelectedAxis}]", StringComparison.OrdinalIgnoreCase));
            if (start == -1) { end = -1; return false; }

            end = Array.FindIndex(lines, start + 1, l => l.TrimStart().StartsWith("[AXIS_", StringComparison.OrdinalIgnoreCase));
            if (end == -1) end = lines.Length;
            return true;
        }

        protected static Dictionary<string, int> ParseHexValues(string[] lines, int start, int end)
        {
            var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = start; i < end; i++)
            {
                string line = lines[i].Trim();
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;

                string key = line.Substring(0, eq).Trim();   // 예: "Pr.228"
                string val = line.Substring(eq + 1).Trim();  // 예: "00FF"
                if (int.TryParse(val, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int parsed))
                {
                    dict[key] = parsed;
                }
            }
            return dict;
        }

        protected static (int high, int low) Split32(int value) =>
            ((value >> 16) & 0xFFFF, value & 0xFFFF);

        protected static int Combine32(int high, int low) =>
            ((high & 0xFFFF) << 16) | (low & 0xFFFF);

        protected static string ReplaceKeepingIndent(string originalLine, string newContent)
        {
            int idx = 0;
            while (idx < originalLine.Length && char.IsWhiteSpace(originalLine[idx])) idx++;
            string indent = originalLine.Substring(0, idx);
            return indent + newContent;
        }

        /// <summary>
        /// 섹션 범위에서 지정 키(대소문자 무시)를 찾아 "Pr.xxx = {value:X4}"로 치환.
        /// 찾지 못하면 false를 반환(나중에 삽입용).
        /// </summary>
        protected static bool TryUpdateKeyHexInRange(List<string> lines, int start, int end, string key, int hexValue)
        {
            for (int i = start; i < end; i++)
            {
                string raw = lines[i];
                string trimmed = raw.TrimStart();
                int eq = trimmed.IndexOf('=');
                if (eq <= 0) continue;

                string k = trimmed.Substring(0, eq).Trim();
                if (k.Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    lines[i] = ReplaceKeepingIndent(raw, $"{key} = {hexValue:X4}");
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 섹션 끝 위치(end)에 키들을 순서대로 삽입하고 end를 증가시켜 최신화.
        /// </summary>
        protected static void InsertKeysAtEnd(List<string> lines, ref int end, params (string key, int hexValue)[] kvs)
        {
            int insertAt = end;
            foreach (var (key, value) in kvs)
            {
                lines.Insert(insertAt++, $"{key} = {value:X4}");
            }
            end = insertAt;
        }

        protected void WriteToIniWithSectionUpdate()
        {
            try
            {
                if (!ValidatePathAndAxis(out var readLines, out int start, out int end))
                {
                    MessageBox.Show("INI 파일 또는 AXIS 정보가 유효하지 않습니다.");
                    return;
                }

                var lines = new List<string>(readLines);
                WriteToIniCore(lines, start, ref end);

                File.WriteAllLines(IniPath, lines.ToArray());

                _suppressRead = true;
                ReadFromIni();
                _suppressRead = false;

                MessageBox.Show("INI 파일 업데이트 완료");
            }
            catch (Exception ex)
            {
                MessageBox.Show("INI 쓰기 오류: " + ex.Message);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
