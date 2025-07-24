using PropertyChanged;
using Support;
using Support.Data;
using Support.Logger;
using Support.Wpf.Models;
using System.Windows.Input;
using Ivi.Visa;
using Ivi.Visa.FormattedIO;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using Support.Wpf;
using System.Windows.Media;
namespace WpfApp_TestOmron
{
    [AddINotifyPropertyChangedInterface]
    public class Model_Main
    {
        public static Dispatcher? DispMain;
        public string IP_Port { get; set; } = "192.168.0.1:9600";
        public string WriteData { get; set; } = "*IDN?";
        public bool EnConnect { get; set; } = true;
        public string TextConnect { get; set; } = "連線";

        public static string[] StrSteps = { 
            "等待探針到位", "燒錄處理", "等待電測程序",
            "3V,uA 電表測試" , "等待電表汽缸上升", "LED閃爍檢測" , "頻譜儀天線強度測試",
            "5V,mA 電表測試", "DIO探針(指撥1)LED檢測", "開關探針(指撥1)LED檢測",
            "開蓋按鈕LED檢測", "磁簧汽缸LED檢測", "2.4V LED閃爍檢測", "完成並記錄資訊"
        };
        
        public ObservableCollection<string> VISA_Devices = [];
        public string DeviceVISA { get; set; } = "";
        public ObservableCollection<StepData> StepsData { get; set; } = [];
        public Dictionary<int, StepData?> MapSteps { get; set; } = [];
        public IMessageBasedSession? Session { get; set; }
        public ICommand Command_Refresh { get; set; }
        public ICommand Command_ConnectSwitch { get; set; }
        public ICommand Command_Write { get; set; }

        //public TcpClientApp TcpConnect { get; set; } = new();
        private bool isRefreshing = false;
        private bool IsConnected = false;
        MessageBasedFormattedIO? FormattedIO = null;
        public Model_Main()
        {
            ///重要: 使用這個函式庫需要先安裝 Library Suite
            ///https://www.keysight.com/tw/zh/lib/software-detail/computer-software/io-libraries-suite-downloads-2175637.html
            Command_Refresh = new RelayCommand<object>((obj)=>{
                Task.Run(() =>
                {
                    "裝置刷新".TryCatch(() =>
                    {
                        if (isRefreshing) return;
                        isRefreshing = true;
                        var dev_list = GlobalResourceManager.Find("TCPIP?*inst?*INSTR");

                        DispMain?.Invoke(() => {
                            VISA_Devices = new(dev_list);
                            SysLog.Add(LogLevel.Info, $"已獲取裝置清單: {VISA_Devices.Count}個裝置");
                        });
                        isRefreshing = false;
                    });
                });
            });
            Command_ConnectSwitch = new RelayCommand<object>((obj) =>
            {
                $"裝置{TextConnect}".TryCatch(() => {
                    if (IsConnected)
                    {
                        Session?.Dispose();
                        FormattedIO = null;
                        Session = null;
                        EnConnect = true;
                        TextConnect = "連線";
                        IsConnected = false;
                        return;
                    }
                    else
                    {
                        EnConnect = false;
                        TextConnect = "連線中";
                        Task.Run(() =>
                        {
                            try
                            {
                                Session = GlobalResourceManager.Open(DeviceVISA) as IMessageBasedSession;
                                FormattedIO = new MessageBasedFormattedIO(Session);
                                DispMain?.Invoke(() =>
                                {
                                    EnConnect = true;
                                    TextConnect = "斷線";
                                    IsConnected = true;
                                });
                            }
                            catch(Exception)
                            {
                                SysLog.Add(LogLevel.Error, "連線超時");
                                EnConnect = true;
                                TextConnect = "連線";
                                IsConnected = false;
                            }
                        });
                    }
                    if (string.IsNullOrEmpty(DeviceVISA))
                        return;
                });

            });
            Command_Write = new RelayCommand<object>((obj) =>
            {
                "命令".TryCatch(() =>
                {
                    FormattedIO?.WriteLine(WriteData);
                    SysLog.Add(LogLevel.Success, $"已接收: {FormattedIO?.ReadLine()}");
                });
            });
            int sid = 1;
            foreach (var step in StrSteps)
            {
                StepData stepData = new() { ID = sid, ColorBrush = Brushes.LightBlue, Title = step };
                StepsData.Add(stepData);
                MapSteps.Add(sid, stepData);
                sid++;
            }
            Task.Run(() =>
            {
                Thread.Sleep(1000);
                Command_Refresh.Execute(null);
            });
        }
       
    }

   
}
