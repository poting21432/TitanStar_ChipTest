using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Support.Wpf
{
    public enum Status
    {
        Stop, Completed, Error, Processing, Paused
    }
    /// <summary>
    /// UserControl_StatusIndicator.xaml 的互動邏輯
    /// </summary>
    public partial class UserControl_StatusIndicator : UserControl
    {
        public Duration Duration
        {
            get { return (Duration)GetValue(DurationProperty); }
            set { SetValue(DurationProperty, value); }
        }

        public static readonly DependencyProperty DurationProperty =
            DependencyProperty.Register("Duration", typeof(Duration), typeof(UserControl_StatusIndicator), new PropertyMetadata(default(Duration)));

        public Brush SpinnerColor
        {
            get { return (Brush)GetValue(SpinnerColorProperty); }
            set { SetValue(SpinnerColorProperty, value); }
        }

        public static readonly DependencyProperty SpinnerColorProperty =
            DependencyProperty.Register("SpinnerColor", typeof(Brush), typeof(UserControl_StatusIndicator), new PropertyMetadata(Brushes.DodgerBlue));

        public double IconSize
        {
            get { return (double)GetValue(IconSizeProperty); }
            set { SetValue(IconSizeProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IconSize.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IconSizeProperty =
            DependencyProperty.Register("IconSize", typeof(double), typeof(UserControl_StatusIndicator), new PropertyMetadata(20.0));

        public Status Status
        {
            get { return (Status)GetValue(StatusProperty); }
            set { SetValue(StatusProperty, value);}
        }

        // Using a DependencyProperty as the backing store for IconSize.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty StatusProperty =
            DependencyProperty.Register("Status", typeof(Status), typeof(UserControl_StatusIndicator), new PropertyMetadata(Status.Stop,OnStatusChanged));

        public UserControl_StatusIndicator()
        {
            InitializeComponent();
        }
        private static void OnStatusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
           
        }
    }
}
