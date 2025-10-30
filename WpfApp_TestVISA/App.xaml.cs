using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using Support.ThreadHelper;
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
        }
        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {

        }

    }
}
