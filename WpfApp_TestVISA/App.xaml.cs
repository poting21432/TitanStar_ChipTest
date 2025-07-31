using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Controls;

namespace WpfApp_TestOmron
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

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {

        }

    }
}
