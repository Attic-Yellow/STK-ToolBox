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
        #region Binding Properties

        private string _iniPath = @"D:\LBS_DB\ServoParameter.ini";
        /// <summary>ServoParameter.ini 전체 경로</summary>
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
        /// <summary>[AXIS_xxx] 형식의 대상 섹션 이름</summary>
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
        /// <summary>전체 AXIS 개수 (UI 참고용)</summary>
        public int AxisCount
        {
            get => _axisCount;
            set
            {
                if (_axisCount == value) return;
                _axisCount = value;
                OnPropertyChanged();
            }
        }

        /// <summary>읽기 중 재귀 호출 방지 플래그</summary>
        protected bool _suppressRead;

        #endregion

        #region Abstract Core (Override in Child)

        /// <summary>
        /// 파생 클래스가 INI 읽기 처리(상태 갱신/Notify 포함)를 구현.
        /// lines: 전체 파일, start~end: 대상 섹션 범위
        /// 섹션이 없거나 오류 시: lines = Array.Empty&lt;string&gt;(), start = end = 0 으로 전달.
        /// </summary>
        protected abstract void ReadFromIniCore(string[] lines, int start, int end);

        /// <summary>
        /// 파생 클래스가 INI 쓰기 처리(해당 섹션 범위에서 PR.x 갱신)를 구현.
        /// lines: 수정 가능한 List, start: 섹션 시작 인덱스, end: 섹션 끝(다음 섹션 시작) – 필요시 ref로 업데이트
        /// </summary>
        protected abstract void WriteToIniCore(List<string> lines, int start, ref int end);

        #endregion

        #region Public INI Read/Write Entry

        /// <summary>
        /// IniPath / SelectedAxis 기준으로 섹션을 찾아 파생 클래스에 읽기 처리를 위임.
        /// </summary>
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

        /// <summary>
        /// 섹션을 찾은 뒤, WriteToIniCore를 호출하여 내용 갱신 후 파일을 다시 저장.
        /// 완료 후 다시 ReadFromIni() 호출하여 바인딩 상태를 갱신.
        /// </summary>
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

        #endregion

        #region INI Section Locate (Path & Axis Validation)

        /// <summary>
        /// IniPath 및 SelectedAxis가 유효한지 확인하고,
        /// 전체 lines와 [SelectedAxis] 섹션의 start~end 인덱스를 반환.
        /// 실패 시 false 반환.
        /// </summary>
        protected bool ValidatePathAndAxis(out string[] lines, out int start, out int end)
        {
            lines = null;
            start = end = -1;

            if (string.IsNullOrWhiteSpace(IniPath) ||
                !File.Exists(IniPath) ||
                string.IsNullOrWhiteSpace(SelectedAxis))
                return false;

            lines = File.ReadAllLines(IniPath);

            // [AXIS_xxx] 섹션 시작
            start = Array.FindIndex(
                lines,
                l => l.Trim().Equals($"[{SelectedAxis}]", StringComparison.OrdinalIgnoreCase));

            if (start == -1)
            {
                end = -1;
                return false;
            }

            // 다음 [AXIS_..] 섹션 시작 위치
            end = Array.FindIndex(
                lines,
                start + 1,
                l => l.TrimStart().StartsWith("[AXIS_", StringComparison.OrdinalIgnoreCase));

            if (end == -1)
                end = lines.Length;

            return true;
        }

        #endregion

        #region Hex Parse / Combine Helpers

        /// <summary>
        /// 섹션 범위 내의 Pr.xxx = HEX 값들을 Dictionary에 파싱하여 반환.
        /// key: "Pr.228" 형식, value: int(16진 해석 값)
        /// </summary>
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

        /// <summary>32비트를 상위/하위 16비트로 분리.</summary>
        protected static (int high, int low) Split32(int value) =>
            ((value >> 16) & 0xFFFF, value & 0xFFFF);

        /// <summary>상위/하위 16비트를 하나의 32비트 값으로 결합.</summary>
        protected static int Combine32(int high, int low) =>
            ((high & 0xFFFF) << 16) | (low & 0xFFFF);

        #endregion

        #region Line Replace / Insert Helpers

        /// <summary>
        /// 기존 줄의 들여쓰기를 유지하면서, 내용만 newContent로 교체.
        /// </summary>
        protected static string ReplaceKeepingIndent(string originalLine, string newContent)
        {
            int idx = 0;
            while (idx < originalLine.Length && char.IsWhiteSpace(originalLine[idx])) idx++;
            string indent = originalLine.Substring(0, idx);
            return indent + newContent;
        }

        /// <summary>
        /// 섹션 범위에서 지정 키(대소문자 무시)를 찾아
        /// "Pr.xxx = {value:X4}"로 치환.
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

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        #endregion
    }
}
