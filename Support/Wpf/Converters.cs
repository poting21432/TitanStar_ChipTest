using Support.Data;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Support.Wpf
{
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return boolValue ? Visibility.Visible : Visibility.Collapsed;

            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibilityValue)
                return visibilityValue == Visibility.Visible;
            return DependencyProperty.UnsetValue;
        }
    }
    [ValueConversion(typeof(Status), typeof(Visibility))]
    public class StatusToVisibilityConverter : IValueConverter
    {
        public Status TargetStatus { get; set; } = Status.Stop;
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Status statusValue)
                return (statusValue == TargetStatus)? Visibility.Visible : Visibility.Collapsed;

            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibilityValue)
                return (visibilityValue == Visibility.Visible) ? TargetStatus : DependencyProperty.UnsetValue;
            return DependencyProperty.UnsetValue;
        }
    }
    [ValueConversion(typeof(Status), typeof(bool))]
    public class StatusToBoolConverter : IValueConverter
    {
        public Status TargetStatus { get; set; } = Status.Stop;
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Status statusValue)
                return (statusValue == TargetStatus);

            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibilityValue)
                return (visibilityValue == Visibility.Visible);
            return DependencyProperty.UnsetValue;
        }
    }
    public class EnumDescriptionTypeConverter : EnumConverter
    {
        public EnumDescriptionTypeConverter(Type type)
            : base(type)
        {
        }
        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string))
            {
                if (value != null)
                {
                    FieldInfo? fi = value.GetType().GetField(value.ToString() ?? "");
                    if (fi != null)
                    {
                        var attributes = (DescriptionAttribute[])fi.GetCustomAttributes(typeof(DescriptionAttribute), false);
                        return ((attributes.Length > 0) && 
                            (!string.IsNullOrEmpty(attributes[0].Description))) 
                                ? attributes[0].Description : value.ToString() ?? "";
                    }
                }

                return string.Empty;
            }

            return base.ConvertTo(context, culture, value, destinationType) ?? new();
        }
    }
    public class ConverterRatio : IValueConverter
    {
        /// <summary>
        /// Value <=> Value * N / D
        /// </summary>
        
        public double Numerator { get; set; }
        public double Denominator { get; set; }
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (double)value * Numerator / Denominator;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => (double)value * Denominator / Numerator;
    }
    public class StringItemSourceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string str = (string)value;
            return str.Split(' ');
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.Join(' ', (string[])value);
        }
    }
    public class BoolToColorConverter : IValueConverter
    {
        public Brush? BrushTrue { get; set; } = Brushes.Green;
        public Brush? BrushFalse { get; set; } = Brushes.Red;
        
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool boolValue = (bool)value;
            return (boolValue ? BrushTrue : BrushFalse) ?? Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Brush brush = (Brush)value;
            return (brush?.ToString() == BrushTrue?.ToString()) ? true : false;
        }
    }

    public class DoubleFormatConverter : IValueConverter
    {
        public string Formatter { get; set; } = "F3";
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            =>value.ToDouble().ToString(Formatter);
        

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value?.ToString()?.ToDouble() ?? double.NaN;
    }


    public class PointFormatConverter : IValueConverter
    {
        public string Formatter { get; set; } = "F3";
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Point point)
                return string.Format("{0},{1}", point.X.ToString("F3"), point.Y.ToString("F3"));
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string[] d = value.ToString()?.Split(',') ?? Array.Empty<string>();
            if(d.Length == 2)
            {
                double.TryParse(d[0].ToString(), out double x);
                double.TryParse(d[1].ToString(), out double y); 
                return new Point(x, y);
            }
            return value;
        }
    }
}
