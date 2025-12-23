using DeviceDB;
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
using System;
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

        internal bool IsStop { get; set; } = false;
        public int CurrentStepID = 0;

        private string PathBatBurn = "";

        public bool IsBusy { get; set; } = false;
        public bool IsBusyBurn { get; set; } = false;
        public ObservableCollection<string> AssignedTests { get; set; } = ["燒錄bat呼叫", "頻譜儀天線測試", "3V-uA電表測試", "5V-mA電表測試", "LED 閃爍計數檢測"];

        public ObservableCollection<string> VISA_Devices { get; set; } = [];
        public ObservableCollection<ProductRecord> ProductRecords { get; set; } = [];
        public string DeviceVISA { get; set; } = "";
        public ObservableCollection<string> ProductTypes { get; set; } = ["G51", "ZBRT"];
        private string selectedProductType = "G51";
        public string SelectedProductType
        {
            get => selectedProductType;
            set
            {
                selectedProductType = value;
                if(value == "G51")
                    InitSteps(StrSteps_G51);
                else if(value =="ZBRT")
                    InitSteps(StrSteps_ZBRT);

            }
        }
        public ObservableCollection<Instruction> Instructions { get; set; } = [];
        public ObservableCollection<Instruction> InstructionsBurn { get; set; } = [];
        public ObservableCollection<StepData> StepsData { get; set; } = [];
        public Dictionary<int, StepData?> MapSteps { get; set; } = [];
        public IMessageBasedSession? Session { get; set; }
        public ICommand Command_Refresh { get; set; }
        public ICommand Command_ConnectSwitch { get; set; }
        public ICommand Command_Write { get; set; }
        public ICommand Commnad_MainSequence { get; set; }
        public ICommand Commnad_BurnSequence { get; set; }
        public ICommand Commnad_MainReset { get; set; }
        public ICommand Commnad_BurnReset { get; set; }
        public ICommand Commnad_NextStepBurn { get; set; }
        public ICommand Command_SetPowerMeter_High { get; set; }
        public ICommand Command_SetPowerMeter_Low { get; set; }
        public ICommand Command_TestPowerMeter_Low { get; set; }
        public ICommand Command_TestPowerMeter_High { get; set; }
        public ICommand Command_TestBurn { get; set; }

        public ICommand Command_PhotoresistTest { get; set; }
        public ObservableCollection<string> BurnTypes { get; set; } = ["PathBAT_G51", "PathBAT_ZBRT"];
        public ObservableCollection<PLCData> PLCAddrData { get; set; } = [];
        public PLCAddr? SelectedPLCAddr { get; set; }
        public string SelectedBurnType { get; set; } = "PathBAT_G51";

        public ProductRecord? CurrentProduct { get; set; }
        //public TcpClientApp TcpConnect { get; set; } = new();
        private bool isRefreshing = false;
        private bool IsConnected = false;
        MessageBasedFormattedIO? FormattedIO = null;
        public bool IsModeStep { get; set; } = false;
        public bool IsModeStepBurn { get; set; } = false;
        internal bool SignalNext = false;
        internal bool SignalNextBurn = false;

        public bool IsManualMode { get; set; } = false;
        public Model_Main()
        {
            Task.Run(() =>
            {
                while (!Global.IsInitialized)
                    Thread.Sleep(500);
                DispMain?.Invoke(() =>
                {
                    PLCAddrData.Clear();
                    foreach(var addr in Global.PLCAddrs.Values)
                    {
                        PLCData data = new() { Id = addr.Id, Address = addr.Address, Title = addr.Title};
                        PLCAddrData.Add(data);
                    }
                });
            });
            Task.Run(() =>
            {
                string[] PLCAddrList = [];
                while (!Global.IsInitialized || PLCAddrData.Count != Global.PLCAddrs.Values.Count)
                    Thread.Sleep(500);

                PLCAddrList = PLCAddrData.Select(x => x.Address ?? "").ToArray();
                "PLC同步".TryCatch(() =>
                {
                    while (true)
                    {
                        Thread.Sleep(500);
                        var result = Global.PLC.ReadRandomData(PLCAddrList);
                        DispMain?.Invoke(() =>
                        {
                            using (Dispatcher.CurrentDispatcher.DisableProcessing())
                            {
                                for(int i =0;i< PLCAddrData.Count; i++)
                                {
                                    if (i < result.ReturnValues.Length)
                                        PLCAddrData[i].Status = result.ReturnValues[i];
                                }
                                    
                            }
                        });
                    }
                });
            });
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
                if(SelectedProductType == "G51")
                    ProcedureMain_G51();
                else if(SelectedProductType == "ZBRT")
                    ProcedureMain_ZBRT();
            });
            Commnad_BurnSequence = new RelayCommand<object>((obj) =>
            {
                if (SelectedProductType == "G51")
                    ProcedureBurn_G51();
                else if(SelectedProductType == "ZBRT")
                    ProcedureBurn_ZBRT();
            });
            Commnad_MainReset = new RelayCommand<object>((obj) =>
            {
                Task.Run(()=> ProcedureReset());
            });
            Commnad_BurnReset = new RelayCommand<object>((obj) =>
            {
                Task.Run(() => ProcedureBurnReset());
            });
            Commnad_NextStepBurn = new RelayCommand<object>((obj) =>
            {
                SignalNextBurn = true;
            });
            Command_SetPowerMeter_Low = new RelayCommand<object>((obj) =>
            {
                Instruction ins1 =(new(1, "切電表至低量程", Order.SendModbus, [(ushort)0x001F, (ushort)1])
                {
                    OnStart = (Ins) => SysLog.Add(LogLevel.Info, "設定電表至低量程:(0x001F)->1")
                });
                ins1.Execute();
                
            });
            Command_SetPowerMeter_High = new RelayCommand<object>((obj) =>
            {
                Instruction ins1 = new(1, "切電表至高量程", Order.SendModbus, [(ushort)0x001F, (ushort)2])
                {
                    OnStart = (Ins) => SysLog.Add(LogLevel.Info, "設定電表至高量程:(0x001F)->2")
                };
                ins1.Execute();
            });
            Command_TestPowerMeter_Low = new RelayCommand<object>((obj) =>
            {
                Instruction ins1 = new(1, "檢查電表為低量程", Order.WaitModbus, [(ushort)0x36, (ushort)0, 5000])
                {
                    OnStart = (Ins) => SysLog.Add(LogLevel.Info, "檢查電表為低量程:(0x36) == 0"),
                    OnEnd = (Ins) =>
                    {
                        if (Ins.ExcResult == ExcResult.Success)
                            SysLog.Add(LogLevel.Info, "已確認電表: 低量程");
                        else
                            SysLog.Add(LogLevel.Error, "電表量程檢查失敗");
                    }
                };
                ins1.Execute();
                if (ins1.ExcResult == ExcResult.Error)
                    return;
                Instruction ins2 =(new(2, "讀電表值(低量程)", Order.ReadModbusFloat, [(ushort)0x30])
                {
                    OnStart = (Ins) => SysLog.Add(LogLevel.Info, "讀取低量程電表數值:(0x30)"),
                    OnEnd = (Ins) =>
                    {
                        float? uA = Ins.Result as float?;
                        if (!uA.HasValue)
                            Ins.ExcResult = ExcResult.Error;
                        if (Ins.ExcResult == ExcResult.Success)
                            SysLog.Add(LogLevel.Info, $"讀取電表數值(低量程):{Ins.Result}");
                        else
                            SysLog.Add(LogLevel.Error, "電表數值異常");
                    }
                });
                ins2.Execute();
            });
            Command_TestPowerMeter_High = new RelayCommand<object>((obj) =>
            {
                Instruction ins1 = (new(2, "檢查電表為高量程", Order.WaitModbus, [(ushort)0x36, (ushort)1, 5000])
                {
                    OnStart = (Ins) => SysLog.Add(LogLevel.Info, "檢查電表為高量程:(0x36) == 1"),
                    OnEnd = (Ins) =>
                    {
                        if (Ins.ExcResult == ExcResult.Success)
                            SysLog.Add(LogLevel.Info, "已確認電表: 高量程");
                        else
                            SysLog.Add(LogLevel.Error, "電表量程檢查失敗");
                    }
                });
                ins1.Execute();
                if (ins1.ExcResult == ExcResult.Error)
                    return;
                Instruction ins2 = (new(1, "讀電表值(高量程)", Order.ReadModbusFloat, [(ushort)0x32])
                {
                    OnStart = (Ins) => SysLog.Add(LogLevel.Info, "讀取高量程電表數值:(0x32)"),
                    OnEnd = (Ins) =>
                    {
                        float? uA = Ins.Result as float?;
                        if (!uA.HasValue)
                            Ins.ExcResult = ExcResult.Error;
                        if (Ins.ExcResult == ExcResult.Success)
                            SysLog.Add(LogLevel.Info, $"讀取電表數值(高量程):{Ins.Result}");
                        else
                            SysLog.Add(LogLevel.Error, "電表數值異常");
                    }
                });
                ins2.Execute();
            });
            Command_TestBurn = new RelayCommand<object>((obj) =>
            {
                Instruction ins =(new(6, "燒錄", Order.Burn, SelectedBurnType) {
                });
                Task.Run(() => ins.Execute());
            });
            Command_PhotoresistTest = new RelayCommand<object>((obj) =>
            {
                Task.Run(() =>
                {
                    string photoresistor = "M7000";//Test
                    Instruction ins = new(1, $"閃爍檢測4次", Order.PLCSignalCount, [Global.PLC, photoresistor, (short)4, 10000])
                    {
                        OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"開始閃爍檢測 4次: M7000 ^v 4"),
                        OnEnd = (Ins) =>
                        {
                            if (Ins.ExcResult != ExcResult.Success)
                                SysLog.Add(LogLevel.Error, "閃爍檢測計數錯誤");
                            else
                            {
                                SysLog.Add(LogLevel.Info, $"確認閃爍4次");
                                Thread.Sleep(1000);
                            }
                        }
                    };
                    ins.Execute();
                });
            });
            InitSteps(StrSteps_G51);
            Task.Run(() =>
            {
                Thread.Sleep(1000);
                Command_Refresh.Execute(null);
            });
        }
        bool IsReseting = false;
        bool IsBurnReseting = false;
        public void ProcedureReset()
        {
            if (IsReseting)
                return;
            IsReseting = true;

            while(IsBusy)
            {
                SysLog.Add(LogLevel.Warning, "等待測試程序結束...");
                Thread.Sleep(5000);
            }
            //電路
            Global.PLC.WriteOneData("M3011", 0);//2.4V
            Global.PLC.WriteOneData("M3010", 0);
            Global.PLC.WriteOneData("M3003", 0);//5V
            Global.PLC.WriteOneData("M3004", 0);
            Global.PLC.WriteOneData("M3008", 0);//3V
            Global.PLC.WriteOneData("M3009", 0);
            Thread.Sleep(1000);
            //汽缸
            Global.PLC.WriteOneData("M3014", 0);
            Global.PLC.WriteOneData("M3006", 0);
            Thread.Sleep(1000);
            //開關
            Global.PLC.WriteOneData("M3001", 0);
            Global.PLC.WriteOneData("M3002", 0);
            Global.PLC.WriteOneData("M3005", 0);
            Thread.Sleep(500);

            if (Global.PLC.ReadOneData("M4000").ReturnValue != 0)
            {
                SysLog.Add(LogLevel.Info, "確認升降汽缸在上定位");
                Global.PLC.WriteOneData("M3012", 0);
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
        public void ProcedureBurnReset()
        {
            if (IsBurnReseting)
                return;
            IsBurnReseting = true;

            while (IsBusyBurn)
            {
                SysLog.Add(LogLevel.Warning, "等待燒錄 程序結束...");
                Thread.Sleep(5000);
            }
            if (Global.PLC.ReadOneData("M4010").ReturnValue != 0)
            {
                SysLog.Add(LogLevel.Info, "確認燒錄升降汽缸在上定位");
                Global.PLC.WriteOneData("M3020", 0);
                Thread.Sleep(500);
                SysLog.Add(LogLevel.Info, "燒錄升降汽缸下降 M3021 -> 1");
                Global.PLC.WriteOneData("M3021", 1);
                Thread.Sleep(1000);
                while (Global.PLC.ReadOneData("M4011").ReturnValue == 0)
                    Thread.Sleep(500);
                SysLog.Add(LogLevel.Info, "確認燒錄升降汽缸已在下定位 M4011 == 1");
                Global.PLC.WriteOneData("M3021", 0);
                SysLog.Add(LogLevel.Info, "燒錄升降汽缸下降復歸 M3021 -> 0");
            }
            IsBurnReseting = false;
        }
        private void AddSigCount(int ID,string Memory, short Count, Action? ExtAction = null)
        {
            /*
            Instructions.Add(new(ID, $"閃爍檢測{Count}次-Bypass", Order.Custom)
            {
                OnStart = (ins) => Thread.Sleep(1000),
                OnEnd = (ins) => SysLog.Add(LogLevel.Info, "閃爍檢測{Count}次-Bypass")
            });
            return;
            */
            Instructions.Add(new(ID, $"閃爍檢測{Count}次", Order.PLCSignalCount, [Global.PLC,Memory, (short)Count, 5000])
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
                if (CurrentStepID <= StepsData.Count)
                {
                    MapSteps.TryGetValue(CurrentStepID, out step);
                    step?.SetStart();
                }
            });
        }
        internal void ResetSteps()
        {
            DispMain?.Invoke(() =>
            {
                CurrentStepID = 0;
                foreach (var step in StepsData)
                    step.Reset();
            });
        }
        internal void InitSteps(string[] StrSteps)
        {
            DispMain?.Invoke(() =>
            {
                StepsData.Clear();
                MapSteps.Clear();
                int sid = 1;
                foreach (var step in StrSteps)
                {
                    StepData stepData = new() { ID = sid, ColorBrush = Brushes.LightBlue, Title = step };
                    StepsData.Add(stepData);
                    MapSteps.Add(sid, stepData);
                    sid++;
                }
                ResetSteps();
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
    [AddINotifyPropertyChangedInterface]
    public class PLCData : PLCAddr
    {
        public short? Status { get; set; }
        public static ICommand CommandSetTrue { get; set; } = new RelayCommand<PLCData>((data) => SetValue(data.Address, 1));
        public static ICommand CommandSetFalse { get; set; } = new RelayCommand<PLCData>((data) => SetValue(data.Address, 0));
        public PLCData()
        {
        }
        static void SetValue(string? Address, short value)
        {
            if (string.IsNullOrEmpty(Address))
            {
                SysLog.Add(LogLevel.Error, "位置為空");
                return;
            }
            Task.Run (()=> Global.PLC.WriteOneData(Address, value));
        }
    }
}
