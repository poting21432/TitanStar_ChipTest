using Support;
using Support.Files;
using Support.Logger;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
namespace WpfApp_TestOmron
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {

            Model_Main.DispMain = Dispatcher;
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            SysLog.Add(LogLevel.Info, "程式啟動");

            Instruction ins = new(1, "Test", Order.Burn);
            ins.Execute();
        }
    }
}