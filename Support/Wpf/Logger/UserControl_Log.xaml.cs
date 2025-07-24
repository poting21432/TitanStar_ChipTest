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
    /// <summary>
    /// UserControl_Log.xaml 的互動邏輯
    /// </summary>
    public partial class UserControl_Log : UserControl
    {
        public string Title { get; set; } = "";
        public string sTag { get; set; } = "";
        public UserControl_Log()
        {
            InitializeComponent();
            this.DataContext = this;
            SysLog.BindControl(RichTextBox_Log);
        }
    }
}
