using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace STK_ToolBox.Helpers
{
    /// <summary>
    /// 값이 null → Collapsed  
    /// 값이 있음 → Visible  
    /// 로 변환하는 단순 Visibility 컨버터.
    /// 
    /// 사용 목적:
    /// - 객체 유무에 따라 UI 표시/숨김을 자동 제어하고 싶을 때 활용.
    /// - 예: 선택된 항목이 없으면 UI를 감추는 경우 등.
    ///
    /// XAML 예:
    /// <TextBlock Text="상세정보"
    ///            Visibility="{Binding SelectedItem, Converter={StaticResource NullToVisibility}}"/>
    ///
    /// ※ ConvertBack 은 사용하지 않음.
    /// </summary>
    public class NullToVisibilityConverter : IValueConverter
    {
        #region ───────── Convert (object → Visibility) ─────────

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value == null ? Visibility.Collapsed : Visibility.Visible;

        #endregion

        #region ───────── ConvertBack 미지원 ─────────

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();

        #endregion
    }
}
