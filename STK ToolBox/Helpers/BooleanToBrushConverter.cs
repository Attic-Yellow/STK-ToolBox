using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace STK_ToolBox.Helpers
{
    /// <summary>
    /// Boolean 값을 Brush(색상)으로 변환하는 WPF 전용 IValueConverter.
    /// 
    /// 사용 목적:
    /// - IO 상태(ON/OFF), 장비 연결 여부, 체크 상태 등을 색으로 표현하고 싶을 때 Binding에 사용.
    /// 
    /// True  → TrueBrush (기본 초록색)
    /// False → FalseBrush (기본 빨간색)
    /// 
    /// XAML 사용 예:
    /// <TextBlock Text="ON/OFF"
    ///            Background="{Binding IsOn, Converter={StaticResource BoolBrush}}" />
    /// 
    /// 원하는 색으로 바꾸려면 XAML에서 Brush를 Override:
    /// <helpers:BooleanToBrushConverter x:Key="BoolBrush" 
    ///                                   TrueBrush="LightGreen" 
    ///                                   FalseBrush="LightCoral"/>
    /// </summary>
    public class BooleanToBrushConverter : IValueConverter
    {
        #region ───────── Brush Properties ─────────

        /// <summary>
        /// value == true 일 때 반환되는 Brush.
        /// 기본값: 초록색 (#2ECC71)
        /// </summary>
        public Brush TrueBrush { get; set; } =
            new SolidColorBrush(Color.FromRgb(46, 204, 113));

        /// <summary>
        /// value == false 일 때 반환되는 Brush.
        /// 기본값: 빨간색 (#E74C3C)
        /// </summary>
        public Brush FalseBrush { get; set; } =
            new SolidColorBrush(Color.FromRgb(231, 76, 60));

        #endregion

        #region ───────── IValueConverter 구현 ─────────

        /// <summary>
        /// bool → Brush 변환
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? TrueBrush : FalseBrush;

        /// <summary>
        /// Brush → bool 변환은 지원하지 않음.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();

        #endregion
    }
}
