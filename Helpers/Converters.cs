using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace InspectionApp.Helpers
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is true ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => (Visibility)value == Visibility.Visible;
    }

    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is true ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => (Visibility)value != Visibility.Visible;
    }

    public class RowIndexToBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush Even = new(Color.FromRgb(255, 255, 255));
        private static readonly SolidColorBrush Odd = new(Color.FromRgb(248, 250, 252));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is int i && i % 2 != 0 ? Odd : Even;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
