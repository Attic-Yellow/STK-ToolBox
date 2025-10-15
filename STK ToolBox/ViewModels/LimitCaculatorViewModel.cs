using STK_ToolBox.Helpers; // RelayCommand
using STK_ToolBox.ViewModels.BaseSource;
using System.Globalization;
using System.Windows.Input;

namespace STK_ToolBox.ViewModels
{
    public class LimitCalculatorViewModel : ParamIniViewModelBase
    {
        // 현재값
        private int _softPlus;
        private int _softMinus;

        // 프리뷰
        private int _previewPlus, _previewMinus;

        // 입력값(문자열)
        private string _softLimitPlus, _softLimitPlusDelta, _softLimitMinus, _softLimitMinusDelta;
        public string SoftLimitPlus { get => _softLimitPlus; set { _softLimitPlus = value; OnPropertyChanged(); RecalcPreview(); } }
        public string SoftLimitPlusDelta { get => _softLimitPlusDelta; set { _softLimitPlusDelta = value; OnPropertyChanged(); RecalcPreview(); } }
        public string SoftLimitMinus { get => _softLimitMinus; set { _softLimitMinus = value; OnPropertyChanged(); RecalcPreview(); } }
        public string SoftLimitMinusDelta { get => _softLimitMinusDelta; set { _softLimitMinusDelta = value; OnPropertyChanged(); RecalcPreview(); } }

        // 표시용
        public string CurrentSoftLimitPlusDecimal => _softPlus.ToString();
        public string CurrentSoftLimitPlusHighHex => ((_softPlus >> 16) & 0xFFFF).ToString("X4");
        public string CurrentSoftLimitPlusLowHex => ((_softPlus) & 0xFFFF).ToString("X4");
        public string CurrentSoftLimitMinusDecimal => _softMinus.ToString();
        public string CurrentSoftLimitMinusHighHex => ((_softMinus >> 16) & 0xFFFF).ToString("X4");
        public string CurrentSoftLimitMinusLowHex => ((_softMinus) & 0xFFFF).ToString("X4");

        public string PreviewSoftLimitPlusDecimal => _previewPlus.ToString();
        public string PreviewSoftLimitPlusHighHex => ((_previewPlus >> 16) & 0xFFFF).ToString("X4");
        public string PreviewSoftLimitPlusLowHex => ((_previewPlus) & 0xFFFF).ToString("X4");
        public string PreviewSoftLimitMinusDecimal => _previewMinus.ToString();
        public string PreviewSoftLimitMinusHighHex => ((_previewMinus >> 16) & 0xFFFF).ToString("X4");
        public string PreviewSoftLimitMinusLowHex => ((_previewMinus) & 0xFFFF).ToString("X4");

        public ICommand ApplyCommand { get; }
        public ICommand ZeroParamsCommand { get; }

        public LimitCalculatorViewModel()
        {
            ApplyCommand = new RelayCommand(() => Apply());
            ZeroParamsCommand = new RelayCommand(() => Zero());
            ReadFromIni(); // 초기 1회
        }

        protected override void ReadFromIniCore(string[] lines, int start, int end)
        {
            _softPlus = 0; _softMinus = 0;

            if (lines.Length > 0)
            {
                var dict = ParseHexValues(lines, start, end);
                dict.TryGetValue("Pr.228", out int p228);
                dict.TryGetValue("Pr.229", out int p229);
                dict.TryGetValue("Pr.22A", out int p22A);
                dict.TryGetValue("Pr.22B", out int p22B);

                _softPlus = Combine32(p229, p228);
                _softMinus = Combine32(p22B, p22A);
            }

            NotifyCurrent();
            RecalcPreview();
        }

        private void NotifyCurrent()
        {
            OnPropertyChanged(nameof(CurrentSoftLimitPlusDecimal));
            OnPropertyChanged(nameof(CurrentSoftLimitPlusHighHex));
            OnPropertyChanged(nameof(CurrentSoftLimitPlusLowHex));
            OnPropertyChanged(nameof(CurrentSoftLimitMinusDecimal));
            OnPropertyChanged(nameof(CurrentSoftLimitMinusHighHex));
            OnPropertyChanged(nameof(CurrentSoftLimitMinusLowHex));
        }

        private static int TryParseOr(string s, int fallback)
        {
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                return v;
            return fallback;
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

        // ===== 쓰기 =====
        private int _pendingPlus, _pendingMinus;

        protected override void WriteToIniCore(System.Collections.Generic.List<string> lines, int start, ref int end)
        {
            var (highPlus, lowPlus) = Split32(_pendingPlus);
            var (highMinus, lowMinus) = Split32(_pendingMinus);

            bool has228 = TryUpdateKeyHexInRange(lines, start, end, "Pr.228", lowPlus);
            bool has229 = TryUpdateKeyHexInRange(lines, start, end, "Pr.229", highPlus);
            bool has22A = TryUpdateKeyHexInRange(lines, start, end, "Pr.22A", lowMinus);
            bool has22B = TryUpdateKeyHexInRange(lines, start, end, "Pr.22B", highMinus);

            if (!has228 || !has229 || !has22A || !has22B)
            {
                var adds = new System.Collections.Generic.List<(string key, int hexValue)>();
                if (!has228) adds.Add(("Pr.228", lowPlus));
                if (!has229) adds.Add(("Pr.229", highPlus));
                if (!has22A) adds.Add(("Pr.22A", lowMinus));
                if (!has22B) adds.Add(("Pr.22B", highMinus));
                InsertKeysAtEnd(lines, ref end, adds.ToArray());
            }
        }

        private void Apply()
        {
            if (!int.TryParse(SoftLimitPlus, out int plus) ||
                !int.TryParse(SoftLimitPlusDelta, out int deltaPlus) ||
                !int.TryParse(SoftLimitMinus, out int minus) ||
                !int.TryParse(SoftLimitMinusDelta, out int deltaMinus))
            {
                System.Windows.MessageBox.Show("정수 값을 입력하세요.");
                return;
            }

            _pendingPlus = plus + deltaPlus;
            _pendingMinus = minus - deltaMinus;

            WriteToIniWithSectionUpdate();
        }

        private void Zero()
        {
            _pendingPlus = 0;
            _pendingMinus = 0;
            WriteToIniWithSectionUpdate();
        }
    }
}
