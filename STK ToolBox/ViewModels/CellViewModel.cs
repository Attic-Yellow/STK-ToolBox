using System.ComponentModel;
using STK_ToolBox.Models;

namespace STK_ToolBox.ViewModels
{
    /// <summary>
    /// Teaching 맵의 단일 셀(Bank/Bay/Level) 상태를 나타내는 ViewModel.
    /// Dead Cell 여부(IsDead)와 표시용 텍스트(DisplayText)를 제공한다.
    /// </summary>
    public class CellViewModel : INotifyPropertyChanged
    {
        #region Fields

        private bool _isDead;

        #endregion

        #region Properties

        /// <summary>
        /// 셀의 고유 식별자 (Bank, Bay, Level 포함)
        /// </summary>
        public CellIdentifier CellId { get; }

        /// <summary>
        /// Dead Cell 여부
        /// </summary>
        public bool IsDead
        {
            get => _isDead;
            set
            {
                if (_isDead != value)
                {
                    _isDead = value;
                    OnPropertyChanged(nameof(IsDead));
                }
            }
        }

        /// <summary>
        /// UI 표시용 텍스트 (예: "1/3/5")
        /// </summary>
        public string DisplayText => $"{CellId.Bank}/{CellId.Bay}/{CellId.Level}";

        #endregion

        #region Constructor

        public CellViewModel(CellIdentifier id, bool isDead)
        {
            CellId = id;
            _isDead = isDead;
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));

        #endregion
    }
}
