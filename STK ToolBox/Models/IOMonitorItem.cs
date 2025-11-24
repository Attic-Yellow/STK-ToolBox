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
        private bool _hasNote;   // 메모 존재 여부

        public int Id
        {
            get { return _id; }
            set
            {
                if (_id == value) return;
                _id = value;
                OnPropertyChanged();
            }
        }

        public string IOName
        {
            get { return _ioName; }
            set
            {
                if (_ioName == value) return;
                _ioName = value;
                OnPropertyChanged();
            }
        }

        public string Address
        {
            get { return _address; }
            set
            {
                if (_address == value) return;
                _address = value;
                OnPropertyChanged();
            }
        }

        public string Unit
        {
            get { return _unit; }
            set
            {
                if (_unit == value) return;
                _unit = value;
                OnPropertyChanged();
            }
        }

        public string DetailUnit
        {
            get { return _detailUnit; }
            set
            {
                if (_detailUnit == value) return;
                _detailUnit = value;
                OnPropertyChanged();
            }
        }

        public string Description
        {
            get { return _description; }
            set
            {
                if (_description == value) return;
                _description = value;
                OnPropertyChanged();
            }
        }

        // 읽기 상태 표시용
        public bool CurrentState
        {
            get { return _currentState; }
            set
            {
                if (_currentState == value) return;
                _currentState = value;
                OnPropertyChanged();
            }
        }

        // 체크 상태
        public bool IsChecked
        {
            get { return _isChecked; }
            set
            {
                if (_isChecked == value) return;
                _isChecked = value;
                OnPropertyChanged();
            }
        }

        // 메모 존재 여부 (IOCheck 화면에서 메모 버튼 색 변경 용)
        public bool HasNote
        {
            get { return _hasNote; }
            set
            {
                if (_hasNote == value) return;
                _hasNote = value;
                OnPropertyChanged();
            }
        }

        // 출력(Y)만 토글 허용
        public bool CanToggle
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Address))
                    return false;

                char c = char.ToUpperInvariant(Address[0]);
                return c == 'Y';
            }
        }

        // B접점 여부 (표시만 민트색)
        public bool IsBContact
        {
            get
            {
                string s1 = (DetailUnit ?? "").ToUpperInvariant();
                string s2 = (Description ?? "").ToUpperInvariant();
                return s1.Contains("B") || s1.Contains("NC") ||
                       s2.Contains("B접점") || s2.Contains("NC");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propName = null)
        {
            var h = PropertyChanged;
            if (h != null)
                h(this, new PropertyChangedEventArgs(propName));
        }
    }
}
