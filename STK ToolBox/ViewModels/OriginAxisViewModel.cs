using STK_ToolBox.Helpers; // RelayCommand
using STK_ToolBox.ViewModels.BaseSource;
using System;
using System.Windows.Input;

namespace STK_ToolBox.ViewModels
{
    public class OriginAxisViewModel : ParamIniViewModelBase
    {
        // INI 현재값 & 프리뷰
        private int _originAxis;
        private int _previewAxis;

        // 입력
        private string _changeAxis;
        public string ChangeAxis
        {
            get => _changeAxis;
            set
            {
                _changeAxis = value;
                OnPropertyChanged();
                if (int.TryParse(value, out var v)) _previewAxis = v;
                else _previewAxis = _originAxis;
                NotifyPreview();
            }
        }

        // 표시 속성들
        public string CurrentOriginAxisDecimal => _originAxis.ToString();
        public string CurrentOriginAxisHighHex => ((_originAxis >> 16) & 0xFFFF).ToString("X4");
        public string CurrentOriginAxisLowHex => (_originAxis & 0xFFFF).ToString("X4");

        public string PreviewOriginAxisDecimal => _previewAxis.ToString();
        public string PreviewOriginAxisHighHex => ((_previewAxis >> 16) & 0xFFFF).ToString("X4");
        public string PreviewOriginAxisLowHex => (_previewAxis & 0xFFFF).ToString("X4");

        public ICommand ApplyCommand { get; }
        public ICommand ZeroParamsCommand { get; }

        public OriginAxisViewModel()
        {
            ApplyCommand = new RelayCommand(() => Apply());
            ZeroParamsCommand = new RelayCommand(() => Zero());
            ReadFromIni(); // 초기 1회
        }

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
                var kvs = new System.Collections.Generic.List<(string key, int hexValue)>();
                if (!has246) kvs.Add(("Pr.246", low));
                if (!has247) kvs.Add(("Pr.247", high));
                InsertKeysAtEnd(lines, ref end, kvs.ToArray());
            }
        }

        private int _pendingWriteAxis;

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

        private void NotifyPreview()
        {
            OnPropertyChanged(nameof(PreviewOriginAxisDecimal));
            OnPropertyChanged(nameof(PreviewOriginAxisHighHex));
            OnPropertyChanged(nameof(PreviewOriginAxisLowHex));
        }
    }
}
