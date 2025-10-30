using Ivi.Visa;
using Ivi.Visa.FormattedIO;
using Modbus.Device;
using PLC;
using PropertyChanged;
using Support;
using Support.Data;
using Support.Logger;
using Support.Wpf;
using Support.Wpf.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO.Ports;
using System.Net;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WpfApp_TestVISA;
namespace WpfApp_TestVISA
{
    [AddINotifyPropertyChangedInterface]
    public partial class Model_Main
    {
        public static Dispatcher? DispMain;
        public string IP_Port { get; set; } = "192.168.0.1:9600";
        public string WriteData { get; set; } = "*IDN?";
        public bool EnConnect { get; set; } = true;
        public string TextConnect { get; set; } = "連線";

        public int CurrentStepID = 0;

        public static readonly string[] StrSteps = [
            "等待探針到位", "燒錄處理", "等待電測程序",
            "3V,uA 電表測試", "等待電表汽缸上升", "3V導通 LED閃爍檢測",
            "DIO探針LED檢測", "指撥1 - LED檢測", "指撥2 - LED檢測",
            "蓋開 - LED檢測", "5V,mA 電表測試", "磁簧汽缸 - LED檢測", "2.4V LED閃爍檢測",
            "測試開關 - LED檢測", "頻譜儀天線強度測試", "完成並記錄資訊"
        ];
        private string PathBatBurn = "";

        bool IsBusy { get; set; } = false;
        public ObservableCollection<string> AssignedTests { get; set; } = ["燒錄bat呼叫", "頻譜儀天線測試", "3V-uA電表測試", "5V-mA電表測試", "LED 閃爍計數檢測"];

        public ObservableCollection<string> VISA_Devices { get; set; } = [];
        public ObservableCollection<ProductRecord> ProductRecords { get; set; } = [];
        public string DeviceVISA { get; set; } = "";
        public ObservableCollection<Instruction> Instructions { get; set; } = [];
        public ObservableCollection<StepData> StepsData { get; set; } = [];
        public Dictionary<int, StepData?> MapSteps { get; set; } = [];
        public IMessageBasedSession? Session { get; set; }
        public ICommand Command_Refresh { get; set; }
        public ICommand Command_ConnectSwitch { get; set; }
        public ICommand Command_Write { get; set; }
        public ICommand Commnad_MainSequence { get; set; }
        public ICommand Commnad_MainReset { get; set; }
        public ICommand Commnad_NextStep { get; set; }
        public ProductRecord? CurrentProduct { get; set; }
        //public TcpClientApp TcpConnect { get; set; } = new();
        private bool isRefreshing = false;
        private bool IsConnected = false;
        MessageBasedFormattedIO? FormattedIO = null;

        public bool IsModeStep { get; set; } = true;
        public bool SignalNext = false;
        public Model_Main()
        {
            ///重要: 使用這個函式庫需要先安裝 Library Suite
            ///https://www.keysight.com/tw/zh/lib/software-detail/computer-software/io-libraries-suite-downloads-2175637.html
            Command_Refresh = new RelayCommand<object>((obj) => {
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
                            catch (Exception)
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

            Commnad_MainSequence = new RelayCommand<object>((obj) =>
            {
                ProcedureMain();
            });
            Commnad_MainReset = new RelayCommand<object>((obj) =>
            {
                ProcedureReset();
            });
            Commnad_NextStep = new RelayCommand<object>((obj) =>
            {
                SignalNext = true;
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
        bool IsReseting = false;
        public void ProcedureReset()
        {
            if (IsReseting)
                return;
            IsReseting = true;
            //燒錄
            Global.PLC.WriteOneData("M3007", 0);
            //電路
            Global.PLC.WriteOneData("M3011", 0);//2.4V
            Global.PLC.WriteOneData("M3010", 0);
            Global.PLC.WriteOneData("M3003", 0);//5V
            Global.PLC.WriteOneData("M3004", 0);
            Global.PLC.WriteOneData("M3008", 0);//3V
            Global.PLC.WriteOneData("M3009", 0);
            //汽缸
            Global.PLC.WriteOneData("M3014", 0);
            Global.PLC.WriteOneData("M3006", 0);
            //開關
            Global.PLC.WriteOneData("M3001", 0);
            Global.PLC.WriteOneData("M3002", 0);
            Global.PLC.WriteOneData("M3005", 0);
            if (Global.PLC.ReadOneData("M4000").ReturnValue != 0)
            {
                SysLog.Add(LogLevel.Info, "確認升降汽缸在上定位");
                Thread.Sleep(500);
                SysLog.Add(LogLevel.Info, "升降汽缸下降 M3013 -> 1");
                Global.PLC.WriteOneData("M3013", 1);
                Thread.Sleep(1000);
                while (Global.PLC.ReadOneData("M4001").ReturnValue == 0)
                    Thread.Sleep(500);
                SysLog.Add(LogLevel.Info, "確認汽缸已在下定位 M4001 == 1");
                Global.PLC.WriteOneData("M3013", 0);
                SysLog.Add(LogLevel.Info, "升降汽缸下降復歸 M3013 -> 0");
            }
            IsReseting = false;
        }

       
        private void AddSigCount(int ID,string Memory, short Count, Action? ExtAction = null)
        {
            Instructions.Add(new(ID, $"閃爍檢測{Count}次", Order.PLCSignalCount, [Global.PLC,Memory, Count, 10000])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"開始閃爍檢測 {Count}次: M4003 ^v {Count}"),
                OnEnd = (Ins) =>
                {
                    if (Ins.ExcResult != ExcResult.Success)
                        SysLog.Add(LogLevel.Error, "閃爍檢測計數錯誤");
                    else
                    {
                        SysLog.Add(LogLevel.Info, $"確認閃爍{Count}次");
                        ExtAction?.Invoke();
                        Thread.Sleep(1000);
                    }
                }
            });
        }
        public void NextStep()
        {
            DispMain?.Invoke(() =>
            {
                MapSteps.TryGetValue(CurrentStepID, out var step);
                step?.SetEnd();
                CurrentStepID++;
                if (CurrentStepID < 0)
                    CurrentStepID = 0;
                if (CurrentStepID < StepsData.Count)
                {
                    MapSteps.TryGetValue(CurrentStepID, out step);
                    step?.SetStart();
                }
            });
        }
        public void ResetSteps()
        {
            DispMain?.Invoke(() =>
            {
                CurrentStepID = 0;
                foreach (var step in StepsData)
                    step.Reset();
            });
        }
    }

    public enum ExcResult
    {
        Success,
        Error,
        Abort,
        TimeOut,
        NotSupport
    }
    public enum Order
    {
        Custom,
        WaitPLCSiganl,
        SendPLCSignal,
        Burn,
        PLCSignalCount,
        SendModbus,
        WaitModbus,
        ReadModbusFloat,
    }
    
    [AddINotifyPropertyChangedInterface]
    public class ProductRecord
    {
        public int ID { get; set; }
        public DateTime TimeStart { get; set; } = DateTime.Now;
        public DateTime? TimeEnd { get; set; }
        public string? BurnCheck { get; set; }
        public float? Test3VuA { get; set; }
        public string? OnCheck { get; set; }
        public string? DIOCheck { get; set; }
        public string? Switch1Check { get; set; }
        public string? Switch2Check { get; set; }
        public string? CoverCheck { get; set; }
        public float? Test5VmA { get; set; }
        public string? ReedCheck { get; set; }
        public string? LowVCheck { get; set; }
        public string? OnOffCheck { get; set; }
        public string? TestAntenna { get; set; }
    }
}
