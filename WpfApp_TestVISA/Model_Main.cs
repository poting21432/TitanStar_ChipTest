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
using System.Windows;
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
                    if(ZBRTTestState.IsBusy)
                    {
                        SysLog.Add(LogLevel.Warning, $"程序正在執行，{G51TestState.Title}強制停止復歸");
                        Command_TestReset.Execute(null);
                    }
                    if (ZBRTBurnState.IsBusy)
                    {
                        SysLog.Add(LogLevel.Warning, $"程序正在執行，{G51BurnState.Title}強制停止復歸");
                        Command_BurnReset.Execute(null);
                    }
                    G51TestState.ResetState("G51測試程序");
                    G51BurnState.ResetState("G51燒錄程序");
                }
                else if(value == "ZBRT" && selectedProductType != "ZBRT")
                {
                    InitSteps(StrSteps_ZBRT);
                    if (G51TestState.IsBusy)
                    {
                        SysLog.Add(LogLevel.Warning, $"程序正在執行，{G51TestState.Title}強制停止復歸");
                        Command_TestReset.Execute(null);
                    }
                    if (G51BurnState.IsBusy)
                    {
                        SysLog.Add(LogLevel.Warning, $"程序正在執行，{G51BurnState.Title}強制停止復歸");
                        Command_BurnReset.Execute(null);
                    }
                    G51TestState.ResetState("ZBRT測試程序");
                    G51BurnState.ResetState("ZBRT燒錄程序");
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
        public ICommand Command_TestStop { get; set; }
        public ICommand Command_TestReset { get; set; }
        public ICommand Command_BurnStop { get; set; }
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
        private bool isManualMode = true;
        public bool IsManualMode
        {
            get => isManualMode;
            set 
            {
                if(isManualMode && !value)
                {
                    var dr = MessageBox.Show("進入自動模式並執行復歸?", "警告",
                        MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (dr == MessageBoxResult.Yes)
                    {
                        Task.Run(() => Command_BurnReset?.Execute(null));
                        Task.Run(() => Command_TestReset?.Execute(null));
                    }
                    else return;
                }
                
                if(!isManualMode && (G51TestState.IsBusy || G51BurnState.IsBusy))
                {
                    var dr = MessageBox.Show("進入手動模式並執行復歸?", "警告",
                        MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (dr == MessageBoxResult.Yes)
                    {
                        Task.Run(() => Command_BurnReset?.Execute(null));
                        Task.Run(() => Command_TestReset?.Execute(null));
                    }
                    else return;
                }
                isManualMode = value;
            }
        }
        static string G51_v3_pos => "G51_Supply_3V+".GetPLCMem(); //M3008
        static string G51_v3_neg => "G51_Supply_3V-".GetPLCMem(); //M3009
        static string G51_v5_pos => "G51_Supply_5V+".GetPLCMem(); //M3003
        static string G51_v5_neg => "G51_Supply_5V-".GetPLCMem(); //M3004
        static string G51_v_low => "G51_Supply_2.4V".GetPLCMem(); //M3011
        static string G51_v3_prb => "G51_Supply_3V+Probe".GetPLCMem(); //M3010
        static string ZBRT_v5_pos => "ZBRT_Supply_5V+".GetPLCMem();//M3051
        static string ZBRT_v5_neg => "ZBRT_Supply_5V-".GetPLCMem();//M3052
        static string ZBRT_v3_pos => "ZBRT_Supply_3V+".GetPLCMem();//M3055
        static string ZBRT_v3_neg => "ZBRT_Supply_3V-".GetPLCMem();//M3056
        static string ZBRT_v_low => "ZBRT_Supply_2.4V".GetPLCMem();//M3058
        public Model_Main()
        {
            IsManualMode = true;
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
                        
                        if (value_BG51 == 1 && !G51BurnState.IsBusy && !G51BurnState.IsReseting)
                            ProcedureBurn_G51();
                        else if (value_G51 == 1 && !G51TestState.IsBusy && !G51TestState.IsReseting)
                            ProcedureTest_G51();
                    }
                    else if(SelectedProductType == "ZBRT")
                    {
                        short value_BZBRT = Global.PLC.ReadOneData(memReady_BZBRT).ReturnValue;
                        short value_ZBRT = Global.PLC.ReadOneData(memReady_ZBRT).ReturnValue;
                        if (value_BZBRT == 1 && !G51BurnState.IsBusy && !G51BurnState.IsReseting)
                            ProcedureBurn_ZBRT();
                        else if (value_ZBRT == 1 && !G51TestState.IsBusy && !G51TestState.IsReseting)
                            ProcedureTest_ZBRT();
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
                
                //SCPI
                SysLog.Add(LogLevel.Info, $"頻譜儀命令:{WriteData}");
                string[] recv = await Global.TcpCommand!.SendAndReceiveTokenAsync(WriteData +"\r\n", "SCPI>");

                if (WriteData.Contains('?'))
                {
                    if (recv.Length >= 2)
                    {
                        string result = recv[1].Trim().Replace("\r", "").Replace("\n", "").Replace("SCPI>", "");
                        SysLog.Add(LogLevel.Info, $"頻譜儀回應:{result}");
                    }
                    else
                    {
                        string result = recv[0].Replace("\n", "").Trim().Split("\r")[0];
                        SysLog.Add(LogLevel.Info, $"頻譜儀回應:{result}");
                    }
                }
            });

            Commnad_MainSequence = new RelayCommand<object>((obj) =>
            {
                if(SelectedProductType == "G51")
                    ProcedureTest_G51();
                else if(SelectedProductType == "ZBRT")
                    ProcedureTest_ZBRT();
            });
            Commnad_BurnSequence = new RelayCommand<object>((obj) =>
            {
                if (SelectedProductType == "G51")
                    ProcedureBurn_G51();
                else if(SelectedProductType == "ZBRT")
                    ProcedureBurn_ZBRT();
            }); 
            Command_TestStop = new RelayCommand<object>((obj) =>
            {
                G51TestState.IsStop = true;
            });
            Command_TestReset = new RelayCommand<object>((obj) =>
            {
                Task.Run(() =>
                {
                    if (SelectedProductType == "G51")
                        ProcedureG51ResetTest();
                    else if (SelectedProductType == "ZBRT")
                        ProcedureZBRTResetTest();
                });
            });
            Command_BurnStop = new RelayCommand<object>((obj) =>
            {
                G51BurnState.IsStop = true;
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
                G51BurnState.SignalNext = true;
            });
            Command_NextStepTest = new RelayCommand<object>((obj) =>
            {
                G51TestState.SignalNext = true;
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
                await PrepareRF();
            });
            Command_ReadN9000B = new RelayCommand<object>(async (obj) =>
            {
                await ReadRFValue();
            });
            InitSteps(StrSteps_G51);
            Task.Run(() =>
            {
                Thread.Sleep(1000);
                Command_Refresh.Execute(null);
            });
        }
        private static async Task<bool> PrepareRF()
        {
            ///*RST 重置資料 //提前做
            ///*RCL 1 命令Recall File 1
            ///:INIT:CONT OFF 停用量測
            try
            {
                if (!Global.TcpCommand?.IsConnected ?? true)
                {
                    bool isC = await Global.LinkSignalAnalyzer();
                    if (!isC) return false;
                }
                await Global.TcpCommand!.SendAndReceiveTokenAsync("*RST\r\n", "SCPI");
                SysLog.Add(LogLevel.Info, "頻譜儀重置");
                //Thread.Sleep(1000);
                await Global.TcpCommand.SendAndReceiveTokenAsync("*RCL 1\r\n", "SCPI");
                SysLog.Add(LogLevel.Info, "頻譜儀切換程序(Recall):1");

                await Global.TcpCommand.SendAndReceiveTokenAsync(":INIT:CONT OFF\r\n", "SCPI");
                Thread.Sleep(500);
                SysLog.Add(LogLevel.Warning, "頻譜儀準備完成");
                return true;
            }
            catch (Exception ex)
            {
                SysLog.Add(LogLevel.Error, $"頻譜儀通訊異常:{ex.Message}");
                return false;
            }
        }
        private static async Task<double> ReadRFValue()
        {
            ///:ABORt 中斷下一次輪詢
            ///:FETC:BPOW? 做完後
            try
            {
                if (!Global.TcpCommand?.IsConnected ?? true)
                {
                    bool isC = await Global.LinkSignalAnalyzer();
                    if (!isC) return double.NaN;
                }
                await Global.TcpCommand!.ClearReadBuffer(Global.CtsTCP.Token);
                string[] ret1 = await Global.TcpCommand.SendAndReceiveTokenAsync(":ABOR\r\n", "SCPI", Global.CtsTCP.Token);
                SysLog.Add(LogLevel.Info, "讀取開始");
                string[] ret2 = await Global.TcpCommand.SendAndReceiveTokenAsync(":FETC:BPOW?\r\n", "SCPI", Global.CtsTCP.Token);
                if(ret2.Length >= 2)
                {
                    string[] data = ret2[1].Split(',');
                    SysLog.Add(LogLevel.Info, $"頻譜儀回應: 輸出{data[2].ToDouble():F3} dBm");
                    return data[2].ToDouble();
                }
                else SysLog.Add(LogLevel.Error, $"頻譜儀回應: 無檢測數據");
                return double.NaN;
            }
            catch(Exception ex)
            {
                SysLog.Add(LogLevel.Error, $"頻譜儀通訊異常:{ex.Message}");
                return double.NaN;
            }
            
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
        Null,
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
