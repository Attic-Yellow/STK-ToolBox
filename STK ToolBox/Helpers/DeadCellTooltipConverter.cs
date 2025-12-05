using System;
using System.Globalization;
using System.Windows.Data;

namespace STK_ToolBox.Helpers
{
    /// <summary>
    /// DeadCell 여부(bool) → Tooltip 문자열로 변환하는 WPF Converter.
    /// 
    /// true  → "Dead Cell (비활성 셀)"
    /// false → "활성 셀"
    /// 
    /// TeachingSheetGenerator에서 DeadCell을 시각적으로 표시하거나,
    /// DataGrid Tooltip 등에 DeadCell 상태를 자연어로 표기할 때 사용한다.
    /// 
    /// 사용 예:
    /// <Border ToolTip="{Binding IsDead, Converter={StaticResource DeadCellTooltip}}" />
    /// </summary>
    public class DeadCellTooltipConverter : IValueConverter
    {
        #region ───────── Convert (bool → string) ─────────

        /// <summary>
        /// DeadCell 여부에 따라 Tooltip 문자열 반환.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is bool b && b)
                ? "Dead Cell (비활성 셀)"
                : "활성 셀";
        }

        #endregion

        #region ───────── ConvertBack 지원 안함 ─────────

        /// <summary>
        /// Tooltip → bool 변환은 필요하지 않아 NotImplemented.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
