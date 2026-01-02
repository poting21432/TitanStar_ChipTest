using DeviceDB;
using Keysight.Visa;
using Modbus.Device;
using PLC;
using PropertyChanged;
using Support;
using Support.Data;
using Support.Logger;
using Support.Net;
using Support.Wpf;
using Support.Wpf.Models;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.DirectoryServices.ActiveDirectory;
using System.IO.Ports;
using System.Net;
using System.Resources;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WpfApp_TestOmron;
using WpfApp_TestVISA;
using ResourceManager = Keysight.Visa.ResourceManager;
namespace WpfApp_TestVISA
{
    [AddINotifyPropertyChangedInterface]
    public partial class Model_Main
    {
        public static Dispatcher? DispMain;
        public string WriteData { get; set; } = "*IDN?";
        public bool EnConnect { get; set; } = true;
        public string TextConnect { get; set; } = "連線";

        public int CurrentStepID = 0;

        private string PathBatBurn = "";
        public int BurnRetryT { get; set; } = 3;
        public string ZBRT_Supply { get; set; } = "OFF";
        public string G51_Supply { get; set; } = "OFF";
        public ObservableCollection<string> AssignedTests { get; set; } = ["燒錄bat呼叫", "頻譜儀天線測試", "3V-uA電表測試", "5V-mA電表測試", "LED 閃爍計數檢測"];
        public ObservableCollection<string> VISA_Devices { get; set; } = [];
        public ObservableCollection<ProductRecord> ProductRecords { get; set; } = [];
        public string DeviceVISA { get; set; } = "";
        public ObservableCollection<string> ProductTypes { get; set; } = ["G51", "ZBRT"];
        private string? selectedProductType = "";
        public string? SelectedProductType
        {
            get => selectedProductType;
            set
            {
                if(value == "G51" && selectedProductType!="G51")
                {
                    InitSteps(StrSteps_G51);
                    if(TestState.IsBusy)
                    {
                        SysLog.Add(LogLevel.Warning, $"程序正在執行，{TestState.Title}強制停止復歸");
                        Command_MainReset.Execute(null);
                    }
                    if (BurnState.IsBusy)
                    {
                        SysLog.Add(LogLevel.Warning, $"程序正在執行，{BurnState.Title}強制停止復歸");
                        Command_BurnReset.Execute(null);
                    }
                    TestState.ResetState("G51測試程序");
                    BurnState.ResetState("G51燒錄程序");
                }
                else if(value == "ZBRT" && selectedProductType != "ZBRT")
                {
                    InitSteps(StrSteps_ZBRT);
                    if (TestState.IsBusy)
                    {
                        SysLog.Add(LogLevel.Warning, $"程序正在執行，{TestState.Title}強制停止復歸");
                        Command_MainReset.Execute(null);
                    }
                    if (BurnState.IsBusy)
                    {
                        SysLog.Add(LogLevel.Warning, $"程序正在執行，{BurnState.Title}強制停止復歸");
                        Command_BurnReset.Execute(null);
                    }
                    TestState.ResetState("ZBRT測試程序");
                    BurnState.ResetState("ZBRT燒錄程序");
                }
                selectedProductType = value;
            }
        }
        public ObservableCollection<Instruction> Instructions { get; set; } = [];
        public ObservableCollection<Instruction> InstructionsBurn { get; set; } = [];
        public ObservableCollection<StepData> StepsData { get; set; } = [];
        public Dictionary<int, StepData?> MapSteps { get; set; } = [];
        
        public ICommand Command_Refresh { get; set; }
        public ICommand Command_ConnectSwitch { get; set; }
        public ICommand Command_Write { get; set; }
        public ICommand Commnad_MainSequence { get; set; }
        public ICommand Commnad_BurnSequence { get; set; }
        public ICommand Command_G51_3VON { get; set; }
        public ICommand Command_G51_5VON { get; set; }
        public ICommand Command_G51_PrbON { get; set; }
        public ICommand Command_G51_LowV { get; set; }
        public ICommand Command_G51_OFF { get; set; }
        public ICommand Command_ZBRT_3VON { get; set; }
        public ICommand Command_ZBRT_5VON { get; set; }
        public ICommand Command_ZBRT_LowV { get; set; }
        public ICommand Command_ZBRT_OFF { get; set; }
        public ICommand Command_TestN9000B { get; set; }
        public ICommand Command_ReadN9000B { get; set; }
        public ICommand Command_MainReset { get; set; }
        public ICommand Command_BurnReset { get; set; }
        public ICommand Command_NextStepBurn { get; set; }
        public ICommand Command_NextStepTest { get; set; }
        public ICommand Command_SetPowerMeter_High { get; set; }
        public ICommand Command_SetPowerMeter_Low { get; set; }
        public ICommand Command_TestPowerMeter_Low { get; set; }
        public ICommand Command_TestPowerMeter_High { get; set; }
        public ICommand Command_TestBurn { get; set; }
        public ObservableCollection<string> BurnTypes { get; set; } = ["PathBAT_G51", "PathBAT_ZBRT"];
        public ObservableCollection<PLCData> PLCAddrData { get; set; } = [];
        public PLCAddr? SelectedPLCAddr { get; set; }
        public string SelectedBurnType { get; set; } = "PathBAT_G51";

        public ProductRecord? CurrentProduct { get; set; }
        //public TcpClientApp TcpConnect { get; set; } = new();
        private bool isRefreshing = false;
        private bool IsConnected = false;

        public bool IsManualMode { get; set; } = true;
        public Model_Main()
        {
            Task.Run(() =>
            {
                while (!Global.IsInitialized)
                    Thread.Sleep(500);
                string memReady_G51 = "G51_Signal_Ready".GetPLCMem();
                string memReady_ZBRT = "ZBRT_Signal_Ready".GetPLCMem();
                string memReady_BG51= "G51_Burn_Ready".GetPLCMem();
                string memReady_BZBRT = "ZBRT_Burn_Ready".GetPLCMem();
                while (true)
                {
                    Thread.Sleep(500);
                    if (IsManualMode)
                        continue;
                    if(SelectedProductType == "G51")
                    {
                        short value_BG51 = Global.PLC.ReadOneData(memReady_BG51).ReturnValue;
                        short value_G51 = Global.PLC.ReadOneData(memReady_G51).ReturnValue;
                        
                        if (value_BG51 == 1 && !BurnState.IsBusy)
                            ProcedureBurn_G51();
                        else if (value_G51 == 1 && !TestState.IsBusy)
                            ProcedureMain_G51();
                    }
                    else if(SelectedProductType == "ZBRT")
                    {
                        short value_BZBRT = Global.PLC.ReadOneData(memReady_BZBRT).ReturnValue;
                        short value_ZBRT = Global.PLC.ReadOneData(memReady_ZBRT).ReturnValue;
                        if (value_BZBRT == 1 && !BurnState.IsBusy)
                            ProcedureBurn_ZBRT();
                        else if (value_ZBRT == 1 && !TestState.IsBusy)
                            ProcedureMain_ZBRT();
                    }
                }
            });
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
                /*
                Task.Run(() =>
                {
                    "裝置刷新".TryCatch(() =>
                    {
                        if (isRefreshing) return;
                        isRefreshing = true;
                        string[] dev_list = Global.KeysightManager.Find("?*INSTR").ToArray();
                        //"TCPIP?*inst?*INSTR"

                        DispMain?.Invoke(() => {
                            VISA_Devices = new(dev_list);
                            SysLog.Add(LogLevel.Info, $"已獲取裝置清單: {VISA_Devices.Count}個裝置");
                        });
                    },()=>isRefreshing = false);
                });//*/
            });
            Command_ConnectSwitch = new RelayCommand<object>((obj) =>
            {
                $"裝置{TextConnect}".TryCatch(() => {
                    if (IsConnected)
                    {
                        DispMain?.Invoke(() =>
                        {
                            EnConnect = true;
                            TextConnect = "連線";
                            IsConnected = false;
                        });
                        return;
                    }
                    else
                    {
                        DispMain?.Invoke(() =>
                        {
                            EnConnect = false;
                            TextConnect = "連線中";
                        });
                        try
                        {
                            DispMain?.Invoke(() =>
                            {
                                EnConnect = true;
                                TextConnect = "斷線";
                                IsConnected = true;
                            });
                        }
                        catch (Exception ex)
                        {
                            SysLog.Add(LogLevel.Error, $"連線錯誤{ex.Message}");
                            DispMain?.Invoke(() =>
                            {
                                EnConnect = true;
                                TextConnect = "連線";
                                IsConnected = false;
                            });
                        }
                    }
                    if (string.IsNullOrEmpty(DeviceVISA))
                        return;
                });

            }); 
            
            Command_Write = new RelayCommand<object>(async (obj) =>
            {

                SysLog.Add(LogLevel.Info, $"頻譜儀命令:{WriteData}");
                string[] recv = await Global.TcpCommand.SendAndReceiveSequenceAsync(WriteData +"\r\n", 2);
                if (WriteData.Contains("?") && recv.Length >= 2)
                {
                    string result = recv[1].Trim().Replace("\r", "").Replace("\n", "");
                    SysLog.Add(LogLevel.Info, $"頻譜儀回應:{result}");
                }
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
            Command_MainReset = new RelayCommand<object>((obj) =>
            {
                Task.Run(() =>
                {
                    if (SelectedProductType == "G51")
                        ProcedureG51ResetTest();
                    else if (SelectedProductType == "ZBRT")
                        ProcedureZBRTResetTest();
                });
            });
            Command_BurnReset = new RelayCommand<object>((obj) =>
            {
                Task.Run(() =>
                {
                    if (SelectedProductType == "G51")
                        ProcedureG51ResetBurn();
                    else if (SelectedProductType == "ZBRT")
                        ProcedureZBRTResetBurn();
                });
            });
            Command_NextStepBurn = new RelayCommand<object>((obj) =>
            {
                BurnState.SignalNext = true;
            });
            Command_NextStepTest = new RelayCommand<object>((obj) =>
            {
                TestState.SignalNext = true;
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
                if (ins1.ExcResult != ExcResult.Success)
                    return;
                Instruction ins2 =(new(2, "讀電表值(低量程)", Order.ReadModbusFloat, [(ushort)0x0032])
                {
                    OnStart = (Ins) => SysLog.Add(LogLevel.Info, "讀取低量程電表數值:(0x32)"),
                    OnEnd = (Ins) =>
                    {
                        float? uA = Ins.Result as float?;
                        if (!uA.HasValue)
                            Ins.ExcResult = ExcResult.Error;
                        if (Ins.ExcResult == ExcResult.Success)
                            SysLog.Add(LogLevel.Info, $"讀取電表數值(低量程):{Ins.Result}uA");
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
                if (ins1.ExcResult != ExcResult.Success)
                    return;
                Instruction ins2 = (new(1, "讀電表值(高量程)", Order.ReadModbusFloat, [(ushort)0x0030])
                {
                    OnStart = (Ins) => SysLog.Add(LogLevel.Info, "讀取高量程電表數值:(0x30)"),
                    OnEnd = (Ins) =>
                    {
                        float? uA = Ins.Result as float?;
                        if (!uA.HasValue)
                            Ins.ExcResult = ExcResult.Error;
                        if (Ins.ExcResult == ExcResult.Success)
                            SysLog.Add(LogLevel.Info, $"讀取電表數值(高量程):{Ins.Result}mA");
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
            string G51_v3_pos = "G51_Supply_3V+".GetPLCMem(); //M3008
            string G51_v3_neg = "G51_Supply_3V-".GetPLCMem(); //M3009
            string G51_v5_pos = "G51_Supply_5V+".GetPLCMem(); //M3003
            string G51_v5_neg = "G51_Supply_5V-".GetPLCMem(); //M3004
            string G51_v_low = "G51_Supply_2.4V".GetPLCMem(); //M3011
            string G51_v3_prb = "G51_Supply_3V+Probe".GetPLCMem(); //M3010
            Command_G51_3VON = new RelayCommand<object>((obj) =>
            {
                Global.PLC.WriteRandomData([G51_v5_pos, G51_v5_neg, G51_v_low, G51_v3_prb], [0, 0, 0, 0]);
                Thread.Sleep(300);
                Global.PLC.WriteRandomData([G51_v3_pos, G51_v3_neg], [1, 1]);
                G51_Supply = "3V";
                SysLog.Add(LogLevel.Warning, $"手動切換電壓(G51):{G51_Supply}");
            }); 
            Command_G51_5VON = new RelayCommand<object>((obj) =>
            {
                Global.PLC.WriteRandomData([G51_v3_prb, G51_v_low], [0,0]); 
                Thread.Sleep(300);
                Global.PLC.WriteRandomData([G51_v3_pos, G51_v3_neg, G51_v5_pos, G51_v5_neg], [1, 1, 1, 1]);
                G51_Supply = "5V";
                SysLog.Add(LogLevel.Warning, $"手動切換電壓(G51):{G51_Supply}");
            });
            Command_G51_PrbON = new RelayCommand<object>((obj) =>
            {
                Global.PLC.WriteRandomData([G51_v5_pos, G51_v5_neg, G51_v_low], [0, 0, 0]);
                Thread.Sleep(300);
                Global.PLC.WriteRandomData([G51_v3_pos, G51_v3_neg, G51_v3_prb], [1, 1, 1]);
                Thread.Sleep(100);
                Global.PLC.WriteRandomData([G51_v3_pos], [0]);
                G51_Supply = "探針";
                SysLog.Add(LogLevel.Warning, $"手動切換電壓(G51):{G51_Supply}");
            });
            Command_G51_LowV = new RelayCommand<object>((obj) =>
            {
                Global.PLC.WriteRandomData([G51_v5_pos, G51_v5_neg, G51_v_low], [0, 0, 0]);
                Thread.Sleep(300);
                Global.PLC.WriteRandomData([G51_v3_pos, G51_v3_neg, G51_v3_prb], [1, 1, 1]);
                Thread.Sleep(100);
                Global.PLC.WriteRandomData([G51_v3_pos], [0]);
                Thread.Sleep(100);
                Global.PLC.WriteRandomData([G51_v_low], [1]);
                G51_Supply = "2.4V";
                SysLog.Add(LogLevel.Warning, $"手動切換電壓(G51):{G51_Supply}");
            });

            Command_G51_OFF = new RelayCommand<object>((obj) =>
            {
                Global.PLC.WriteRandomData([G51_v3_pos, G51_v3_neg, G51_v5_pos, G51_v5_neg, G51_v_low, G51_v3_prb],
                                           [0, 0, 0, 0, 0, 0]);
                G51_Supply = "OFF";
                SysLog.Add(LogLevel.Warning, $"手動切換電壓(G51):{G51_Supply}");
            });
            string ZBRT_v5_pos = "ZBRT_Supply_5V+".GetPLCMem();//M3051
            string ZBRT_v5_neg = "ZBRT_Supply_5V-".GetPLCMem();//M3052
            string ZBRT_v3_pos = "ZBRT_Supply_3V+".GetPLCMem();//M3055
            string ZBRT_v3_neg = "ZBRT_Supply_3V-".GetPLCMem();//M3056
            string ZBRT_v_low = "ZBRT_Supply_2.4V".GetPLCMem();//M3058
            Command_ZBRT_3VON = new RelayCommand<object>((obj) =>
            {
                Global.PLC.WriteRandomData([ZBRT_v5_pos, ZBRT_v5_pos, ZBRT_v_low], [0, 0, 0]);
                Thread.Sleep(300);
                Global.PLC.WriteRandomData([ZBRT_v3_pos, ZBRT_v3_neg], [1, 1]);
                ZBRT_Supply = "3V";
                SysLog.Add(LogLevel.Warning, $"手動切換電壓(ZBRT):{ZBRT_Supply}");
            });
            Command_ZBRT_5VON = new RelayCommand<object>((obj) =>
            {
                Global.PLC.WriteRandomData([ZBRT_v_low, ZBRT_v3_pos, ZBRT_v3_neg], [0, 0, 0]);
                Thread.Sleep(300);
                Global.PLC.WriteRandomData([ZBRT_v5_pos, ZBRT_v5_neg], [ 1, 1]);
                ZBRT_Supply = "5V";
                SysLog.Add(LogLevel.Warning, $"手動切換電壓(ZBRT):{ZBRT_Supply}");
            });
            Command_ZBRT_LowV = new RelayCommand<object>((obj) =>
            {
                Global.PLC.WriteRandomData([ZBRT_v5_pos, ZBRT_v5_neg, ZBRT_v_low], [0, 0, 0]);
                Thread.Sleep(300);
                Global.PLC.WriteRandomData([ZBRT_v3_pos, ZBRT_v3_neg], [1, 1]);
                Thread.Sleep(100);
                Global.PLC.WriteRandomData([ZBRT_v_low], [1]);
                ZBRT_Supply = "2.4V";
                SysLog.Add(LogLevel.Warning, $"手動切換電壓(ZBRT):{ZBRT_Supply}");
            });

            Command_ZBRT_OFF = new RelayCommand<object>((obj) =>
            {
                Global.PLC.WriteRandomData([ZBRT_v3_pos, ZBRT_v3_neg, ZBRT_v5_pos, ZBRT_v5_neg, ZBRT_v_low],
                                           [0, 0, 0, 0, 0]);
                ZBRT_Supply = "OFF";
                SysLog.Add(LogLevel.Warning, $"手動切換電壓(ZBRT):{ZBRT_Supply}");
            });


            Command_TestN9000B = new RelayCommand<object>(async (obj) =>
            {
                ///*RST 重置資料 //提前做
                ///*RCL 1 命令Recall File 1
                ///:INIT:CONT OFF 停用量測
                ///:ABORt 中斷下一次輪詢
                ///:FETC:BPOW? 做完後
                await Global.TcpCommand.SendAndReceiveSequenceAsync("*RST\r\n", 2);
                SysLog.Add(LogLevel.Info, "頻譜儀重置");
                //Thread.Sleep(1000);
                await Global.TcpCommand.SendAndReceiveSequenceAsync("*RCL 1\r\n", 2);
                SysLog.Add(LogLevel.Info, "頻譜儀切換程序(Recall):1");
              
                await Global.TcpCommand.SendAndReceiveSequenceAsync(":INIT:CONT OFF\r\n", 2);
                Thread.Sleep(1000);
                SysLog.Add(LogLevel.Warning, "頻譜儀準備完成");
            });
            Command_ReadN9000B = new RelayCommand<object>(async (obj) =>
            {
                ///*RST 重置資料 //提前做
                ///*RCL 1 命令Recall File 1
                ///:INIT:CONT OFF 停用量測
                ///:ABORt 中斷下一次輪詢
                ///:FETC:BPOW? 做完後
                await Global.TcpCommand.SendAndReceiveAsync(":ABOR\r\n");
                SysLog.Add(LogLevel.Info, "讀取開始");

                string[] ret = await Global.TcpCommand.SendAndReceiveSequenceAsync(":FETC:BPOW?\r\n",3);
                string[] data = ret[2].Split(',');
                SysLog.Add(LogLevel.Info, $"頻譜儀回應: 輸出{data[2].ToDouble():F3} dBm");
            });
            InitSteps(StrSteps_G51);
            Task.Run(() =>
            {
                Thread.Sleep(1000);
                Command_Refresh.Execute(null);
            });
        }
        
        public void ProcedureBurnReset()
        {
            ProcedureState PState = BurnState;
            if (PState.IsReseting)
            {
                SysLog.Add(LogLevel.Warning, $"已正在執行終止:{PState.Title}");
                return;
            }
            PState.IsReseting = true;

            while (BurnState.IsBusy)
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
            PState.IsReseting = false;
        }
        private void AddSigCount(int ID,string Memory, short Count, Action? ExtAction = null, int timeOut = 5000)
        {
            /*
            Instructions.Add(new(ID, $"閃爍檢測{Count}次-Bypass", Order.Custom)
            {
                OnStart = (ins) => Thread.Sleep(1000),
                OnEnd = (ins) => SysLog.Add(LogLevel.Info, "閃爍檢測{Count}次-Bypass")
            });
            return;
            //*/
            Instructions.Add(new(ID, $"閃爍檢測{Count}次", Order.PLCSignalCount, [Global.PLC,Memory, (short)Count, timeOut])
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
        public static ICommand CommandSetTrue { get; set; } = new RelayCommand<PLCData>((data) =>
        {
            SysLog.Add(LogLevel.Warning, $"手動控制{data.Title}{data.Address} -> 1");
            SetValue(data.Address, 1); 
        });
        public static ICommand CommandSetFalse { get; set; } = new RelayCommand<PLCData>((data) =>
        {
            SysLog.Add(LogLevel.Warning, $"手動控制{data.Title}{data.Address} -> 0");
            SetValue(data.Address, 0); 
        });
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
