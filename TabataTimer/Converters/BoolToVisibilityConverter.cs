using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TabataTimer.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isVisible = value is bool b && b;
        bool invert = parameter?.ToString()?.Equals("Invert", StringComparison.OrdinalIgnoreCase) == true;
        if (invert) isVisible = !isVisible;
        return isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
