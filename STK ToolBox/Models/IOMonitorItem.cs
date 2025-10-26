using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace STK_ToolBox.Models
{
    public class IOMonitorItem : INotifyPropertyChanged
    {
        private int _id;
        private string _ioName;
        private string _address;
        private string _unit;
        private string _detailUnit;
        private string _description;
        private bool _currentState;
        private bool _isChecked;

        public int Id
        {
            get => _id;
            set { if (_id != value) { _id = value; OnPropertyChanged(); } }
        }

        public string IOName
        {
            get => _ioName;
            set { if (_ioName != value) { _ioName = value; OnPropertyChanged(); } }
        }

        public string Address
        {
            get => _address;
            set { if (_address != value) { _address = value; OnPropertyChanged(); } }
        }

        public string Unit
        {
            get => _unit;
            set { if (_unit != value) { _unit = value; OnPropertyChanged(); } }
        }

        public string DetailUnit
        {
            get => _detailUnit;
            set { if (_detailUnit != value) { _detailUnit = value; OnPropertyChanged(); } }
        }

        public string Description
        {
            get => _description;
            set { if (_description != value) { _description = value; OnPropertyChanged(); } }
        }

        // 표시용(읽기 전용 바인딩이지만 내부에서 값 갱신 시 UI 반영을 위해 Notify)
        public bool CurrentState
        {
            get => _currentState;
            set { if (_currentState != value) { _currentState = value; OnPropertyChanged(); } }
        }

        //  체크 반영은 이 프로퍼티만 바꿔주면 됨
        public bool IsChecked
        {
            get => _isChecked;
            set { if (_isChecked != value) { _isChecked = value; OnPropertyChanged(); } }
        }

        // 출력(Y)만 토글 허용
        public bool CanToggle =>
            !string.IsNullOrWhiteSpace(Address) &&
            char.ToUpperInvariant(Address[0]) == 'Y';

        // B접점(표시만 민트)
        public bool IsBContact
        {
            get
            {
                var s1 = DetailUnit?.ToUpperInvariant() ?? "";
                var s2 = Description?.ToUpperInvariant() ?? "";
                return s1.Contains("B") || s1.Contains("NC") || s2.Contains("B접점") || s2.Contains("NC");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
