using Support.Data;
using Support.IniHelper;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using PropertyChanged;
namespace Support.Wpf
{
    public enum CrosshairType
    {
        None, Cross, Ellipse, Rectangle,
        CrossEllipse,
        CrossRectangle,
    }
    [AddINotifyPropertyChangedInterface]
    public class ImageView
    {
        private IniReadWrite IniHandle = new IniReadWrite();
        private const string Path_Config = "./ui_settings.ini";
        public CrosshairType CrossMode { get; set; } = CrosshairType.Cross;
        
        public Brush? CrosshairsColor { get; set; } = Brushes.Blue;
        public Size CrossSize { get; set; }
            = new Size(200, 200);
        public Point CrossShift { get; set; } = new Point(0, 0);
        public double CrossThickness { get; set; } = 4;

        public ImageView()
        {
            IniHandle.InitIni(Path_Config, (IniData) =>
            {
                if (IniData == null) return;
                Enum.TryParse(IniData.SafeGet("ImageView", "crosshair_mode"), out CrosshairType type);
                CrossMode = type;
                CrosshairsColor = (SolidColorBrush)new BrushConverter().ConvertFromString(
                    IniData.SafeGet("ImageView", "crosshair_color")?.ToString() ?? "Blue");
                CrossSize = Size.Parse(IniData.SafeGet("ImageView", "crosshair_size")?.ToString());
                CrossShift = Point.Parse(IniData.SafeGet("ImageView", "crosshair_shift")?.ToString());

                CrossThickness = double.Parse(IniData.SafeGet("ImageView", "crosshair_thickness")?.ToString() ?? "4");
            });
        }
        /*
         * ;畫面準星型式(None,Cross,Ellipse, Rectangle,CrossEllipse,CrossRectangle)
            crosshair_mode=Cross
            ;準星顏色
            crosshair_color=Blue
            ;準星偏移(pixel)
            crosshair_shift=0,0
            ;輔助準星大小(pixel)
            crosshair_size=100,100
            ;準星線寬(pixel)
            crosshair_thickness=4
         */
    }

    public class ConverterCrossVisibility : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value switch
            {
                CrosshairType.Cross =>
                    targetType == typeof(Line) ? Visibility.Visible : Visibility.Collapsed,
                CrosshairType.Rectangle =>
                    targetType == typeof(Rectangle) ? Visibility.Visible : Visibility.Collapsed,
                CrosshairType.Ellipse =>
                    targetType == typeof(Ellipse) ? Visibility.Visible : Visibility.Collapsed,
                CrosshairType.CrossRectangle=>
                    targetType == typeof(Rectangle) || targetType == typeof(Line) ? Visibility.Visible : Visibility.Collapsed,
                CrosshairType.CrossEllipse =>
                    targetType == typeof(Ellipse) || targetType == typeof(Line),
                _ => Visibility.Collapsed
            };
        }
        public object? ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => "";
    }
}
