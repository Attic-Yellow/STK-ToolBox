using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace STK_ToolBox.ViewModels
{
    /// <summary>
    /// 모든 ViewModel의 기본 클래스입니다.
    /// WPF 바인딩을 위한 INotifyPropertyChanged를 제공하며,
    /// 속성 값 변경 시 UI 갱신을 도와줍니다.
    /// </summary>
    public abstract class BaseViewModel : INotifyPropertyChanged
    {
        #region INotifyPropertyChanged 구현

        /// <summary>
        /// 속성이 변경될 때 호출되는 이벤트입니다.
        /// WPF 바인딩 시스템에 변경 사실을 알려 UI를 갱신합니다.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// PropertyChanged 이벤트를 발생시킵니다.
        /// CallerMemberName 덕분에 호출한 속성 이름을 자동으로 인식합니다.
        /// </summary>
        /// <param name="propertyName">변경된 속성 이름(생략 가능)</param>
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// backing field를 업데이트하고 UI에 변경 내용을 알립니다.
        /// 값이 달라진 경우에만 갱신하여 불필요한 UI 업데이트를 방지합니다.
        /// </summary>
        /// <typeparam name="T">속성 타입</typeparam>
        /// <param name="storage">backing field 참조</param>
        /// <param name="value">새로운 값</param>
        /// <param name="propertyName">속성 이름(자동 지정)</param>
        /// <returns>값이 실제로 변경되었을 경우 true</returns>
        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value))
                return false;

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion
    }
}
