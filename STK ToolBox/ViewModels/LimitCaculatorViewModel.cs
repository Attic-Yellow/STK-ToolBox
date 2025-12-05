using System.Collections.Generic;
using System.Globalization;
using System.Windows.Input;
using STK_ToolBox.Helpers;            // RelayCommand
using STK_ToolBox.ViewModels.BaseSource;

namespace STK_ToolBox.ViewModels
{
    /// <summary>
    /// ServoParameter.ini의 SOFTLIMIT 관련 파라미터(Pr.228/229/22A/22B)를
    /// - 현재값 표시
    /// - 사용자가 입력한 값/상하한 Delta로 미리보기
    /// - 적용(쓰기) / 0으로 초기화
    /// 하는 뷰모델.
    /// </summary>
    public class LimitCalculatorViewModel : ParamIniViewModelBase
    {
        #region Fields - 현재값 / 프리뷰 / Pending 값

        // 현재 INI에서 읽어온 실제 값
        private int _softPlus;
        private int _softMinus;

        // 화면에서 계산된 프리뷰 값
        private int _previewPlus;
        private int _previewMinus;

        // INI 쓰기용 Pending 값
        private int _pendingPlus;
        private int _pendingMinus;

        #endregion

        #region Fields - 입력 문자열

        private string _softLimitPlus;
        private string _softLimitPlusDelta;
        private string _softLimitMinus;
        private string _softLimitMinusDelta;

        #endregion

        #region Properties - 입력 문자열 바인딩

        /// <summary>플러스 리미트 Max Value (사용자 입력)</summary>
        public string SoftLimitPlus
        {
            get { return _softLimitPlus; }
            set
            {
                _softLimitPlus = value;
                OnPropertyChanged();
                RecalcPreview();
            }
        }

        /// <summary>플러스 리미트 상한치 Delta (사용자 입력)</summary>
        public string SoftLimitPlusDelta
        {
            get { return _softLimitPlusDelta; }
            set
            {
                _softLimitPlusDelta = value;
                OnPropertyChanged();
                RecalcPreview();
            }
        }

        /// <summary>마이너스 리미트 Min Value (사용자 입력)</summary>
        public string SoftLimitMinus
        {
            get { return _softLimitMinus; }
            set
            {
                _softLimitMinus = value;
                OnPropertyChanged();
                RecalcPreview();
            }
        }

        /// <summary>마이너스 리미트 하한치 Delta (사용자 입력)</summary>
        public string SoftLimitMinusDelta
        {
            get { return _softLimitMinusDelta; }
            set
            {
                _softLimitMinusDelta = value;
                OnPropertyChanged();
                RecalcPreview();
            }
        }

        #endregion

        #region Properties - 현재값 표시용(Decimal / Hex)

        public string CurrentSoftLimitPlusDecimal { get { return _softPlus.ToString(); } }
        public string CurrentSoftLimitPlusHighHex { get { return ((_softPlus >> 16) & 0xFFFF).ToString("X4"); } }
        public string CurrentSoftLimitPlusLowHex { get { return (_softPlus & 0xFFFF).ToString("X4"); } }

        public string CurrentSoftLimitMinusDecimal { get { return _softMinus.ToString(); } }
        public string CurrentSoftLimitMinusHighHex { get { return ((_softMinus >> 16) & 0xFFFF).ToString("X4"); } }
        public string CurrentSoftLimitMinusLowHex { get { return (_softMinus & 0xFFFF).ToString("X4"); } }

        #endregion

        #region Properties - 프리뷰 표시용(Decimal / Hex)

        public string PreviewSoftLimitPlusDecimal { get { return _previewPlus.ToString(); } }
        public string PreviewSoftLimitPlusHighHex { get { return ((_previewPlus >> 16) & 0xFFFF).ToString("X4"); } }
        public string PreviewSoftLimitPlusLowHex { get { return (_previewPlus & 0xFFFF).ToString("X4"); } }

        public string PreviewSoftLimitMinusDecimal { get { return _previewMinus.ToString(); } }
        public string PreviewSoftLimitMinusHighHex { get { return ((_previewMinus >> 16) & 0xFFFF).ToString("X4"); } }
        public string PreviewSoftLimitMinusLowHex { get { return (_previewMinus & 0xFFFF).ToString("X4"); } }

        #endregion

        #region Commands

        /// <summary>현재 입력/Delta 기준으로 SOFTLIMIT를 INI에 반영</summary>
        public ICommand ApplyCommand { get; private set; }

        /// <summary>SOFTLIMIT를 0으로 초기화</summary>
        public ICommand ZeroParamsCommand { get; private set; }

        #endregion

        #region Constructor

        public LimitCalculatorViewModel()
        {
            ApplyCommand = new RelayCommand(Apply);
            ZeroParamsCommand = new RelayCommand(Zero);

            // 초기 1회 읽기
            ReadFromIni();
        }

        #endregion

        #region Overrides - INI 읽기(ReadFromIniCore)

        /// <summary>
        /// 선택된 AXIS 섹션의 Pr.228/229/22A/22B를 읽어
        /// _softPlus / _softMinus에 반영.
        /// </summary>
        protected override void ReadFromIniCore(string[] lines, int start, int end)
        {
            _softPlus = 0;
            _softMinus = 0;

            if (lines.Length > 0)
            {
                var dict = ParseHexValues(lines, start, end);

                int p228;
                int p229;
                int p22A;
                int p22B;

                dict.TryGetValue("Pr.228", out p228);
                dict.TryGetValue("Pr.229", out p229);
                dict.TryGetValue("Pr.22A", out p22A);
                dict.TryGetValue("Pr.22B", out p22B);

                // + 방향 SOFTLIMIT
                _softPlus = Combine32(p229, p228);
                // - 방향 SOFTLIMIT
                _softMinus = Combine32(p22B, p22A);
            }

            NotifyCurrent();
            RecalcPreview();
        }

        /// <summary>
        /// 현재값 바인딩 프로퍼티들을 한번에 Notify.
        /// </summary>
        private void NotifyCurrent()
        {
            OnPropertyChanged("CurrentSoftLimitPlusDecimal");
            OnPropertyChanged("CurrentSoftLimitPlusHighHex");
            OnPropertyChanged("CurrentSoftLimitPlusLowHex");
            OnPropertyChanged("CurrentSoftLimitMinusDecimal");
            OnPropertyChanged("CurrentSoftLimitMinusHighHex");
            OnPropertyChanged("CurrentSoftLimitMinusLowHex");
        }

        #endregion

        #region Helpers - 파싱 및 프리뷰 계산

        /// <summary>
        /// 문자열을 int로 파싱, 실패하면 fallback 반환.
        /// </summary>
        private static int TryParseOr(string s, int fallback)
        {
            int v;
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v))
                return v;

            return fallback;
        }

        /// <summary>
        /// 입력값 + Delta를 기반으로 프리뷰값(_previewPlus/_previewMinus) 갱신.
        /// </summary>
        private void RecalcPreview()
        {
            int plus = TryParseOr(_softLimitPlus, _softPlus);
            int deltaPlus = TryParseOr(_softLimitPlusDelta, 0);
            int minus = TryParseOr(_softLimitMinus, _softMinus);
            int deltaMinus = TryParseOr(_softLimitMinusDelta, 0);

            _previewPlus = plus + deltaPlus;
            _previewMinus = minus - deltaMinus;

            OnPropertyChanged("PreviewSoftLimitPlusDecimal");
            OnPropertyChanged("PreviewSoftLimitPlusHighHex");
            OnPropertyChanged("PreviewSoftLimitPlusLowHex");
            OnPropertyChanged("PreviewSoftLimitMinusDecimal");
            OnPropertyChanged("PreviewSoftLimitMinusHighHex");
            OnPropertyChanged("PreviewSoftLimitMinusLowHex");
        }

        #endregion

        #region Overrides - INI 쓰기(WriteToIniCore)

        /// <summary>
        /// Pr.228/229/22A/22B를 _pendingPlus/_pendingMinus 값으로 갱신.
        /// 섹션 내에 키가 없으면 end 위치에 새로 삽입.
        /// </summary>
        protected override void WriteToIniCore(List<string> lines, int start, ref int end)
        {
            int highPlus;
            int lowPlus;
            int highMinus;
            int lowMinus;

            var splitPlus = Split32(_pendingPlus);
            var splitMinus = Split32(_pendingMinus);

            highPlus = splitPlus.high;
            lowPlus = splitPlus.low;
            highMinus = splitMinus.high;
            lowMinus = splitMinus.low;

            bool has228 = TryUpdateKeyHexInRange(lines, start, end, "Pr.228", lowPlus);
            bool has229 = TryUpdateKeyHexInRange(lines, start, end, "Pr.229", highPlus);
            bool has22A = TryUpdateKeyHexInRange(lines, start, end, "Pr.22A", lowMinus);
            bool has22B = TryUpdateKeyHexInRange(lines, start, end, "Pr.22B", highMinus);

            if (!has228 || !has229 || !has22A || !has22B)
            {
                var adds = new List<(string key, int hexValue)>();
                if (!has228) adds.Add(("Pr.228", lowPlus));
                if (!has229) adds.Add(("Pr.229", highPlus));
                if (!has22A) adds.Add(("Pr.22A", lowMinus));
                if (!has22B) adds.Add(("Pr.22B", highMinus));

                InsertKeysAtEnd(lines, ref end, adds.ToArray());
            }
        }

        #endregion

        #region Actions - Apply / Zero

        /// <summary>
        /// 사용자 입력값을 검증 후,
        /// plus + deltaPlus / minus - deltaMinus 를 INI에 기록.
        /// </summary>
        private void Apply()
        {
            int plus;
            int deltaPlus;
            int minus;
            int deltaMinus;

            if (!int.TryParse(SoftLimitPlus, out plus) ||
                !int.TryParse(SoftLimitPlusDelta, out deltaPlus) ||
                !int.TryParse(SoftLimitMinus, out minus) ||
                !int.TryParse(SoftLimitMinusDelta, out deltaMinus))
            {
                System.Windows.MessageBox.Show("정수 값을 입력하세요.");
                return;
            }

            _pendingPlus = plus + deltaPlus;
            _pendingMinus = minus - deltaMinus;

            WriteToIniWithSectionUpdate();
        }

        /// <summary>
        /// SOFTLIMIT(+/-)를 모두 0으로 설정하여 INI에 기록.
        /// </summary>
        private void Zero()
        {
            _pendingPlus = 0;
            _pendingMinus = 0;

            WriteToIniWithSectionUpdate();
        }

        #endregion
    }
}
