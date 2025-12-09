using STK_ToolBox.Helpers; // RelayCommand
using STK_ToolBox.ViewModels.BaseSource;
using System;
using System.Windows.Input;

namespace STK_ToolBox.ViewModels
{
    /// <summary>
    /// Origin Axis (Pr.246 / Pr.247) 편집용 ViewModel
    /// INI 읽기/쓰기 + HEX/DEC 변환 + 프리뷰 기능 포함
    /// </summary>
    public class OriginAxisViewModel : ParamIniViewModelBase
    {
        #region ──────────────── 필드 (Fields) ────────────────

        private int _originAxis;       // INI에서 읽어온 원점축 32bit 값
        private int _previewAxis;      // 입력값 기반 프리뷰용 값
        private int _pendingWriteAxis; // 실제 INI에 기록될 값

        #endregion


        #region ──────────────── 바인딩 속성 (Bindable Properties) ────────────────

        private string _changeAxis;
        public string ChangeAxis
        {
            get => _changeAxis;
            set
            {
                _changeAxis = value;
                OnPropertyChanged();

                if (int.TryParse(value, out var v))
                    _previewAxis = v;
                else
                    _previewAxis = _originAxis;

                NotifyPreview();
            }
        }

        // 현재값 표시
        public string CurrentOriginAxisDecimal => _originAxis.ToString();
        public string CurrentOriginAxisHighHex => ((_originAxis >> 16) & 0xFFFF).ToString("X4");
        public string CurrentOriginAxisLowHex => (_originAxis & 0xFFFF).ToString("X4");

        // 프리뷰 표시
        public string PreviewOriginAxisDecimal => _previewAxis.ToString();
        public string PreviewOriginAxisHighHex => ((_previewAxis >> 16) & 0xFFFF).ToString("X4");
        public string PreviewOriginAxisLowHex => (_previewAxis & 0xFFFF).ToString("X4");

        #endregion


        #region ──────────────── 커맨드 (Commands) ────────────────

        public ICommand ApplyCommand { get; }
        public ICommand ZeroParamsCommand { get; }

        #endregion


        #region ──────────────── 생성자 (Constructor) ────────────────

        public OriginAxisViewModel()
        {
            ApplyCommand = new RelayCommand(() => Apply());
            ZeroParamsCommand = new RelayCommand(() => Zero());

            // 초기값 읽기
            ReadFromIni();
        }

        #endregion


        #region ──────────────── INI 읽기 (Read) / 쓰기 (Write) ────────────────

        protected override void ReadFromIniCore(string[] lines, int start, int end)
        {
            _originAxis = 0;

            if (lines.Length > 0)
            {
                var dict = ParseHexValues(lines, start, end);

                dict.TryGetValue("Pr.246", out int low);
                dict.TryGetValue("Pr.247", out int high);

                _originAxis = Combine32(high, low);
            }

            _previewAxis = _originAxis;

            OnPropertyChanged(nameof(CurrentOriginAxisDecimal));
            OnPropertyChanged(nameof(CurrentOriginAxisHighHex));
            OnPropertyChanged(nameof(CurrentOriginAxisLowHex));

            NotifyPreview();
        }


        protected override void WriteToIniCore(System.Collections.Generic.List<string> lines, int start, ref int end)
        {
            var (high, low) = Split32(_pendingWriteAxis);

            bool has246 = TryUpdateKeyHexInRange(lines, start, end, "Pr.246", low);
            bool has247 = TryUpdateKeyHexInRange(lines, start, end, "Pr.247", high);

            if (!has246 || !has247)
            {
                var list = new System.Collections.Generic.List<(string key, int hexValue)>();
                if (!has246) list.Add(("Pr.246", low));
                if (!has247) list.Add(("Pr.247", high));

                InsertKeysAtEnd(lines, ref end, list.ToArray());
            }
        }

        #endregion


        #region ──────────────── 커맨드 처리 메서드 (Apply / Zero) ────────────────

        private void Apply()
        {
            if (!int.TryParse(ChangeAxis, out int axis))
            {
                System.Windows.MessageBox.Show("정수 값을 입력하세요.");
                return;
            }

            _pendingWriteAxis = axis;
            WriteToIniWithSectionUpdate();
        }

        private void Zero()
        {
            _pendingWriteAxis = 0;
            WriteToIniWithSectionUpdate();
        }

        #endregion


        #region ──────────────── 보조 메서드 (Helpers) ────────────────

        private void NotifyPreview()
        {
            OnPropertyChanged(nameof(PreviewOriginAxisDecimal));
            OnPropertyChanged(nameof(PreviewOriginAxisHighHex));
            OnPropertyChanged(nameof(PreviewOriginAxisLowHex));
        }

        #endregion
    }
}
