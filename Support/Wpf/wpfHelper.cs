using Support.Logger;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace Support.Wpf
{
    using Screen = System.Windows.Forms.Screen;
  
    public static partial class WpfExtendMethods
    {
        public static void CenterScreen<T>(this T window,int ScreenID) where T : Window
        { 
            Screen[] screens = Screen.AllScreens;
            if (ScreenID >= 0 && ScreenID < screens.Length)
                ScreenID = 0;


            double screenWidth = screens[ScreenID].Bounds.Width;
            double screenHeight = screens[ScreenID].Bounds.Height;

            // 計算中心位置
            double windowWidth = window.Width;
            double windowHeight = window.Height;

            double centerX = screens[ScreenID].Bounds.Left + (screenWidth - windowWidth) / 2;
            double centerY = screens[ScreenID].Bounds.Top + (screenHeight - windowHeight) / 2;

            // 設定窗口位置
            window.Left = centerX;
            window.Top = centerY;
        }

        public static void SetLocation(this UIElement Element,double Left,double Top)
        {
            Canvas.SetTop(Element, Top);
            Canvas.SetLeft(Element, Left);
        }
        public static HashtableT<string, Brush> SigColor = new HashtableT<string, Brush>()
        {
            { "OK", Brushes.LightGreen },
            { "NG", Brushes.Red }
        };
        public static void SetResult(this TextBlock tb, string result)
        {
            if(SigColor.ContainsKey(result))
            {
                tb.Dispatcher.Invoke(() => {
                    tb.Foreground = SigColor[result];
                });
            }
        }
        public static void AppendColorLine(this RichTextBox rtb, string text, string color = "", bool AutoScroll = false)
        {
            text += "\r";
            try
            {

                TextRange tr = new TextRange(rtb.Document.ContentEnd, rtb.Document.ContentEnd) { Text = text };
                tr.ApplyPropertyValue(TextElement.ForegroundProperty, (!string.IsNullOrEmpty(color)) 
                                        ? new BrushConverter().ConvertFromString(color) : rtb.Foreground);
                if (AutoScroll)
                    rtb.ScrollToEnd();
            }
            catch (FormatException) { }
        }
      
        public static void ForceRefresh(this DataGrid dataGrid)
        {
            foreach (var item in dataGrid.Items)
            {
                var row = (DataGridRow)dataGrid.ItemContainerGenerator.ContainerFromItem(item);
                if (row != null)
                    row.InvalidateVisual();
            }
        }
        public static void SafeClose(this Window w)
        {
            if (w.Dispatcher.CheckAccess())
                w.Close();
            else
                w.Dispatcher.Invoke(DispatcherPriority.Normal, new ThreadStart(w.Close));
        }
    }
}
