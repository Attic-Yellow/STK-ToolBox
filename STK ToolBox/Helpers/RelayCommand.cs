using System;
using System.Windows;      // DependencyProperty.UnsetValue
using System.Windows.Input;

namespace STK_ToolBox.Helpers
{
    /// <summary>
    /// 파라미터(object)를 받는 기본 RelayCommand
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Predicate<object> _canExecute;

        public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
        {
            if (execute == null) throw new ArgumentNullException(nameof(execute));
            _execute = execute;
            _canExecute = canExecute;
        }

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
            add    { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }

    /// <summary>
    /// 파라미터 T를 받는 안전 캐스팅 버전 RelayCommand&lt;T&gt;
    /// (UnsetValue/잘못된 형식이 들어와도 예외 없이 무시/비활성화)
    /// </summary>
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Predicate<T> _canExecute;

        public RelayCommand(Action<T> execute, Predicate<T> canExecute = null)
        {
            if (execute == null) throw new ArgumentNullException(nameof(execute));
            _execute    = execute;
            _canExecute = canExecute;
        }

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
            add    { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        /// <summary>
        /// object → T 안전 변환
        /// </summary>
        private static bool TryCast(object parameter, out T value)
        {
            // 참조형 또는 Nullable<T>는 null 허용
            bool isNullable = !typeof(T).IsValueType || Nullable.GetUnderlyingType(typeof(T)) != null;

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
    }
}
