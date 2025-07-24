using System;
using System.Globalization;
using System.Windows.Data;

namespace WpfApp_Styles.Converters
{
    public class BooleanToDoubleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? 1.0 : 0.0;
            }

            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double dValue)
                return (dValue == 1.0);
            return false;
        }
    }
}
