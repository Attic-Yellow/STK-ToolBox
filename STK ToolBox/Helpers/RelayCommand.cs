using System;
using System.Windows;      // DependencyProperty.UnsetValue
using System.Windows.Input;

namespace STK_ToolBox.Helpers
{
    /// <summary>
    /// 파라미터(object)를 받는 기본 RelayCommand.
    /// 
    /// - Execute: Action<object>
    /// - CanExecute: Predicate<object> (생략 가능)
    /// - DependencyProperty.UnsetValue 방어 포함
    /// - CommandManager.RequerySuggested 연동
    /// 
    /// XAML 예:
    /// <Button Command="{Binding SaveCommand}" CommandParameter="{Binding SelectedItem}" />
    /// </summary>
    public class RelayCommand : ICommand
    {
        #region ───────── Fields ─────────

        private readonly Action<object> _execute;
        private readonly Predicate<object> _canExecute;

        #endregion

        #region ───────── Constructor ─────────

        public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
        {
            if (execute == null) throw new ArgumentNullException(nameof(execute));

            _execute = execute;
            _canExecute = canExecute;
        }

        #endregion

        #region ───────── ICommand 구현 ─────────

        public bool CanExecute(object parameter)
        {
            // WPF가 바인딩 미완료일 때 던지는 sentinel 값 방어
            if (ReferenceEquals(parameter, DependencyProperty.UnsetValue))
                parameter = null;

            return _canExecute == null || _canExecute(parameter);
        }

        public void Execute(object parameter)
        {
            if (ReferenceEquals(parameter, DependencyProperty.UnsetValue))
                parameter = null;

            _execute(parameter);
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        #endregion
    }

    /// <summary>
    /// 형식 매개변수 T를 받는 타입 안전 RelayCommand.
    /// 
    /// - Execute: Action&lt;T&gt;
    /// - CanExecute: Predicate&lt;T&gt; (생략 가능)
    /// - object → T 캐스팅 실패 시:
    ///   - CanExecute: false 반환 → 버튼 비활성화
    ///   - Execute: 조용히 무시(예외 없음)
    /// - DependencyProperty.UnsetValue 방어 포함
    /// 
    /// XAML 예:
    /// <Button Command="{Binding RemoveCommand}" CommandParameter="{Binding SelectedItem}" />
    /// ViewModel:
    /// RemoveCommand = new RelayCommand&lt;MyItem&gt;(item => Remove(item));
    /// </summary>
    public class RelayCommand<T> : ICommand
    {
        #region ───────── Fields ─────────

        private readonly Action<T> _execute;
        private readonly Predicate<T> _canExecute;

        #endregion

        #region ───────── Constructor ─────────

        public RelayCommand(Action<T> execute, Predicate<T> canExecute = null)
        {
            if (execute == null) throw new ArgumentNullException(nameof(execute));

            _execute = execute;
            _canExecute = canExecute;
        }

        #endregion

        #region ───────── ICommand 구현 ─────────

        public bool CanExecute(object parameter)
        {
            if (ReferenceEquals(parameter, DependencyProperty.UnsetValue))
                parameter = null;

            if (_canExecute == null)
                return true;

            T value;
            if (TryCast(parameter, out value))
                return _canExecute(value);

            // 캐스팅 불가면 실행 불가(버튼 비활성)
            return false;
        }

        public void Execute(object parameter)
        {
            if (ReferenceEquals(parameter, DependencyProperty.UnsetValue))
                parameter = null;

            T value;
            if (TryCast(parameter, out value))
            {
                _execute(value);
            }
            // 캐스팅 불가면 조용히 무시(예외 없음)
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        #endregion

        #region ───────── object → T 안전 캐스팅 ─────────

        /// <summary>
        /// object 값을 T로 안전하게 변환.
        /// - 참조형/Nullable&lt;T&gt;는 null 허용
        /// - 형식 불일치 시 false + default(T) 반환
        /// </summary>
        private static bool TryCast(object parameter, out T value)
        {
            // 참조형 또는 Nullable<T>는 null 허용
            bool isNullable = !typeof(T).IsValueType ||
                              Nullable.GetUnderlyingType(typeof(T)) != null;

            if (parameter == null)
            {
                if (isNullable)
                {
                    value = default(T);
                    return true;
                }

                value = default(T);
                return false;
            }

            if (parameter is T)
            {
                value = (T)parameter;
                return true;
            }

            // 형식 불일치
            value = default(T);
            return false;
        }

        #endregion
    }
}
