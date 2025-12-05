using System;
using System.ComponentModel;

namespace STK_ToolBox.Models
{
    /// <summary>
    /// Bank 단위 기본 Teaching 정보 모델.
    /// 
    /// - TeachingSheet 쪽에서 각 Bank별 기준 위치/값을 저장하는 데 사용.
    /// - Bank 번호와 기준 Bay/Level, Base/Hoist/Turn/Fork Teaching값을 보관한다.
    /// - View에서 바인딩해서 값이 바뀌면 즉시 UI에 반영될 수 있도록 INotifyPropertyChanged를 구현.
    /// </summary>
    public class BankInfo : INotifyPropertyChanged
    {
        #region ───────── INotifyPropertyChanged 구현 ─────────

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 프로퍼티 값이 변경되었음을 알리는 헬퍼 메서드.
        /// </summary>
        /// <param name="propertyName">변경된 프로퍼티 이름</param>
        protected void OnPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region ───────── Bank 기본 정보 ─────────

        private int _bankNumber;
        /// <summary>
        /// Bank 번호 (1, 2, 3, ...).
        /// </summary>
        public int BankNumber
        {
            get { return _bankNumber; }
            set
            {
                if (_bankNumber == value) return;
                _bankNumber = value;
                OnPropertyChanged(nameof(BankNumber));
            }
        }

        private int _baseBay;
        /// <summary>
        /// 이 Bank의 Base 기준 Bay 번호.
        /// </summary>
        public int BaseBay
        {
            get { return _baseBay; }
            set
            {
                if (_baseBay == value) return;
                _baseBay = value;
                OnPropertyChanged(nameof(BaseBay));
            }
        }

        private int _baseLevel;
        /// <summary>
        /// 이 Bank의 Base 기준 Level 번호.
        /// </summary>
        public int BaseLevel
        {
            get { return _baseLevel; }
            set
            {
                if (_baseLevel == value) return;
                _baseLevel = value;
                OnPropertyChanged(nameof(BaseLevel));
            }
        }

        #endregion

        #region ───────── Teaching 기준 값 ─────────

        private int _baseValue;
        /// <summary>
        /// Base 축 Teaching 기준 값.
        /// </summary>
        public int BaseValue
        {
            get { return _baseValue; }
            set
            {
                if (_baseValue == value) return;
                _baseValue = value;
                OnPropertyChanged(nameof(BaseValue));
            }
        }

        private int _hoistValue;
        /// <summary>
        /// Hoist 축 Teaching 기준 값.
        /// </summary>
        public int HoistValue
        {
            get { return _hoistValue; }
            set
            {
                if (_hoistValue == value) return;
                _hoistValue = value;
                OnPropertyChanged(nameof(HoistValue));
            }
        }

        private int _turnValue;
        /// <summary>
        /// Turn 축 Teaching 기준 값.
        /// </summary>
        public int TurnValue
        {
            get { return _turnValue; }
            set
            {
                if (_turnValue == value) return;
                _turnValue = value;
                OnPropertyChanged(nameof(TurnValue));
            }
        }

        private int _forkValue;
        /// <summary>
        /// Fork 축 Teaching 기준 값.
        /// </summary>
        public int ForkValue
        {
            get { return _forkValue; }
            set
            {
                if (_forkValue == value) return;
                _forkValue = value;
                OnPropertyChanged(nameof(ForkValue));
            }
        }

        #endregion
    }
}
