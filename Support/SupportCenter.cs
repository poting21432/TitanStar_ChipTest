using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Support.Data;
using Support.Logger;
using Support.Wpf;

namespace Support
{

    public static class Dialog
    {
        public static IProgressService? ShowTaskProgress(double windowWidth = 500, double windowHeight = 300)
        {
            Model_TaskProgress? progress = null;
            Thread t_hint = new(() =>
            {
                UserControl_TaskProgress uc_task = new();
                Window? window = uc_task.CreateWindow();
                if (window == null)
                    progress = null;
                else
                    progress = new(() => window.Dispatcher);
                uc_task.DataContext = progress;
                progress.EventClose = () =>
                    window.Dispatcher.Invoke(() => window.Close());
                window.ShowWindow(default, new(windowWidth, windowHeight), WindowStartupLocation.CenterScreen, WindowState.Normal);
               
            })
            { IsBackground = true };

            t_hint.SetApartmentState(ApartmentState.STA);
            t_hint.Start();
            Thread.Sleep(500);
            return progress;
        }
        public static void ShowWindow<T>(this T? window, Point Location = default, Size size = default,
          WindowStartupLocation startupLocation = WindowStartupLocation.Manual,
          WindowState state = WindowState.Maximized) where T : Window
        {
            if (window == null)
                return;
            window.WindowStyle = WindowStyle.None;
            window.WindowStartupLocation = startupLocation;
            if (size != default)
            {
                window.Width = size.Width;
                window.Height = size.Height;
            }
            if (Location == default)
                window.CenterScreen(0);
            else
            {
                window.Left = Location.X;
                window.Top = Location.Y;
            }
            window.Show();
            window.WindowState = state;
            Dispatcher.Run();
        }

        public static T? TaskShowWindow<T>(Point Location = default, Size size = default,
          WindowStartupLocation startupLocation = WindowStartupLocation.Manual,
          WindowState state = WindowState.Maximized) where T : Window
        {
            T? w_hint = null;
            Thread t_hint = new(() =>
            {
                T? w_hint = (T?)Activator.CreateInstance(typeof(T));
                w_hint?.ShowWindow(Location, size, startupLocation, state);
            })
            { IsBackground = true };

            return w_hint;
        }

        public static Window CreateWindow<T>(this T element) where T : UIElement
            => new() { Content = element };
    }
    /// <summary>
    /// Standard Static Class Ready For Support
    /// </summary>
    public static class SysLog
    {
        private static readonly Logger.Logger log = new(Environment.CurrentDirectory + "\\log.ini");
        public static void RegisterUISyncEvent(Action<LogLevel, string>? action)
            => log.UISyncEvent += action;
        public static void RegisterFileEvent(Action<LogLevel, string>? action)
            => log.FileEvent += action;
        public static void UnregisterUISyncEvent(Action<LogLevel, string>? action)
           => log.UISyncEvent -= action;
        public static void UnregisterFileEvent(Action<LogLevel, string>? action)
           => log.FileEvent -= action;
        public static void BindControl(Control control_log)
            => log.BindControl(control_log);

        public static void Add(LogLevel level, string message, bool showMsgBox = false)
        {
            log.FileEvent?.Invoke(level, message);
            log.UISyncEvent?.Invoke(level, message);
            if(showMsgBox)
            {
                string title = level.GetEnumDescription();
                MessageBoxButton msg_btn = MessageBoxButton.OK;
                MessageBoxImage msg_image = level switch
                {
                    LogLevel.Error => MessageBoxImage.Error,
                    LogLevel.Warning => MessageBoxImage.Warning,
                    _ => MessageBoxImage.Information
                };
                MessageBox.Show(message, title, msg_btn, msg_image);
            }
        }
        public static void ClearLog() => log.ClearLog();
        public static void Dispose() => log.Dispose();
    }
}
