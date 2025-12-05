using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace STK_ToolBox.Models
{
    /// <summary>
    /// IO 모니터링 한 개 라인에 해당하는 모델.
    /// 
    /// - IOMonitoring 테이블의 한 Row와 1:1로 대응하는 개념
    /// - 이름, 주소, 상세 Unit, 설명 등 정적인 메타 정보
    /// - 현재 IO 상태, 체크 여부, 메모 여부 등 동적인 상태를 함께 보관
    /// - View에서 바인딩하여 실시간으로 상태를 표시/갱신하기 위해 INotifyPropertyChanged 구현
    /// </summary>
    public class IOMonitorItem : INotifyPropertyChanged
    {
        #region ───────── Backing Fields ─────────

        private int _id;
        private string _ioName;
        private string _address;
        private string _unit;
        private string _detailUnit;
        private string _description;
        private bool _currentState;
        private bool _isChecked;
        private bool _hasNote;   // 메모 존재 여부

        #endregion

        #region ───────── DB/정적 메타 정보 ─────────

        /// <summary>
        /// IO 항목 고유 ID (DB PK 등).
        /// </summary>
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

        /// <summary>
        /// IO 이름 (예: STK_DOOR_OPEN_SW 등).
        /// </summary>
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

        /// <summary>
        /// IO 주소 (예: X01A0, Y0200).
        /// </summary>
        public string Address
        {
            get { return _address; }
            set
            {
                if (_address == value) return;
                _address = value;
                OnPropertyChanged();
                // Address 변경 시 CanToggle도 영향 받지만,
                // CanToggle은 계산 프로퍼티라 별도 PropertyChanged는 ViewModel에서 필요 시 호출.
            }
        }

        /// <summary>
        /// Unit (대분류 / 실린더명 등).
        /// </summary>
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

        /// <summary>
        /// DetailUnit (소분류 / 세부 축명 등).
        /// </summary>
        public string DetailUnit
        {
            get { return _detailUnit; }
            set
            {
                if (_detailUnit == value) return;
                _detailUnit = value;
                OnPropertyChanged();
                // DetailUnit 변경 시 IsBContact에도 영향이 있을 수 있음
            }
        }

        /// <summary>
        /// 설명(Description). 화면에서 IO 기능 설명용 텍스트.
        /// </summary>
        public string Description
        {
            get { return _description; }
            set
            {
                if (_description == value) return;
                _description = value;
                OnPropertyChanged();
                // Description 변경 시 IsBContact에도 영향이 있을 수 있음
            }
        }

        #endregion

        #region ───────── 런타임 상태 (모니터링/체크/메모) ─────────

        /// <summary>
        /// 현재 IO 상태 (ON/OFF).
        /// - 입력 X : 현재 입력 상태
        /// - 출력 Y : 현재 출력 상태
        /// </summary>
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

        /// <summary>
        /// 체크 여부.
        /// - IOCheck 화면에서 사용자가 이 항목을 확인 완료/중요 등으로 표시할 때 사용.
        /// </summary>
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

        /// <summary>
        /// 메모 존재 여부.
        /// - IOCheck 화면에서 메모 버튼 색상 변경 등 표시용 플래그.
        /// </summary>
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

        #endregion

        #region ───────── 계산 프로퍼티 (View 표시 전용) ─────────

        /// <summary>
        /// 출력(Y)만 토글 허용.
        /// - Address 첫 글자가 'Y'일 때만 true.
        /// - X(입력)는 강제로 ON/OFF 할 수 없는 구조.
        /// </summary>
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

        /// <summary>
        /// B접점 여부.
        /// - DetailUnit 또는 Description에 "B", "NC", "B접점" 등의 키워드가 들어간 경우 true.
        /// - 화면에서 B접점 IO를 민트색 등으로 강조 표시할 때 사용.
        /// </summary>
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

        #endregion

        #region ───────── INotifyPropertyChanged 구현 ─────────

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 프로퍼티 변경 알림 헬퍼.
        /// CallerMemberName을 사용하여 호출한 프로퍼티 이름 자동 전달.
        /// </summary>
        protected void OnPropertyChanged([CallerMemberName] string propName = null)
        {
            var h = PropertyChanged;
            if (h != null)
                h(this, new PropertyChangedEventArgs(propName));
        }

        #endregion
    }
}
