using Support;
using Support.Logger;
using Support.ThreadHelper;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Controls;
namespace WpfApp_TestVISA
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : PrismApplication 
    {
        protected override Window CreateShell()
        {
            return Container.Resolve<MainWindow>();
        }
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ThreadExtensions.CheckApplicationDuplicated();
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                SysLog.Add(LogLevel.Error, $"程式未預期錯誤: {e}");
            };
        }
        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {

        }

    }
}
