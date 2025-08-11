using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace STK_ToolBox.Helpers
{
    public class BooleanToBrushConverter : IValueConverter
    {
        public Brush TrueBrush { get; set; } = new SolidColorBrush(Color.FromRgb(46, 204, 113)); // green
        public Brush FalseBrush { get; set; } = new SolidColorBrush(Color.FromRgb(231, 76, 60)); // red

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? TrueBrush : FalseBrush;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
