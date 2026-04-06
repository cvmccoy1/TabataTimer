using System.Globalization;
using System.Windows.Data;

namespace TabataTimer.Converters;

public class EnumToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (parameter == null) return false;
        return value?.ToString() == parameter.ToString();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is true && parameter != null)
            return Enum.Parse(targetType, parameter.ToString()!);
        return Binding.DoNothing;
    }
}
