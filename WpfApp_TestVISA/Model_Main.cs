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

        internal Dictionary<string, DeviceDisplay> DevStateMap { get; set; } = [];
        public ObservableCollection<DeviceDisplay> DeviceStates { get; set; } = 
        [
            new("PLC"), new("頻譜儀"), new("電表")
        ];

        internal static Dispatcher? DispMain;
        public string WriteData { get; set; } = "*IDN?";
        public bool EnConnect { get; set; } = true;
        public string TextConnect { get; set; } = "連線";

        public int CurrentStepID = 0;

        private string PathBatBurn = "";
        public int BurnRetryT { get; set; } = 1;
        public string ZBRT_Supply { get; set; } = "OFF";
        public string G51_Supply { get; set; } = "OFF";
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
                    CurrentBurnState = G51BurnState;
                    CurrentTestState = G51TestState;
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
                    CurrentBurnState = ZBRTBurnState;
                    CurrentTestState = ZBRTTestState;
                }
                selectedProductType = value;
            }
        }
        public ObservableCollection<Instruction> Instructions { get; set; } = [];
        public ObservableCollection<Instruction> InstructionsBurn { get; set; } = [];
        public ObservableCollection<StepData> StepsData { get; set; } = [];
        public Dictionary<int, StepData?> MapSteps { get; set; } = [];
        public ICommand Command_Write { get; set; }
        public ICommand Commnad_MainSequence { get; set; }
        public ICommand Commnad_BurnSequence { get; set; }
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
        public ProcedureState? CurrentBurnState { get; set; }
        public ProcedureState? CurrentTestState { get; set; }
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
        
       
        public Model_Main()
        {
            IsManualMode = true;
            DevStateMap = DeviceStates.ToDictionary(x => x.Title);
            G51BurnState.ResetState("G51燒錄程序");
            G51TestState.ResetState("G51測試程序");
            ZBRTBurnState.ResetState("ZBRT燒錄程序");
            ZBRTTestState.ResetState("ZBRT測試程序");
           
            Command_Write = new RelayCommand<object>(async (obj) =>
            {
                if (!Global.TcpCommand?.IsConnected ?? true)
                {
                    bool isC = await Global.LinkSignalAnalyzer();
                    if (!isC) return;
                }
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
            DevStateMap.TryGetValue("電表", out DeviceDisplay? PMDisplay);
            Command_SetPowerMeter_Low = new RelayCommand<object>((obj) =>
            {
                PMDisplay?.SetState(DeviceState.Connecting);
                Instruction ins1 =(new(1, "切電表至低量程", Order.SendModbus, [(ushort)0x001F, (ushort)1])
                {
                    OnStart = (Ins) => SysLog.Add(LogLevel.Info, "設定電表至低量程:(0x001F)->1")
                });
                ins1.Execute();
                if(ins1.ExcResult == ExcResult.Success)
                    PMDisplay?.SetState(DeviceState.Connected);
                else PMDisplay?.SetState(DeviceState.Error);
            });
            Command_SetPowerMeter_High = new RelayCommand<object>((obj) =>
            {
                PMDisplay?.SetState(DeviceState.Connecting);
                Instruction ins1 = new(1, "切電表至高量程", Order.SendModbus, [(ushort)0x001F, (ushort)2])
                {
                    OnStart = (Ins) => SysLog.Add(LogLevel.Info, "設定電表至高量程:(0x001F)->2")
                };
                ins1.Execute();
                if (ins1.ExcResult == ExcResult.Success)
                    PMDisplay?.SetState(DeviceState.Connected);
                else PMDisplay?.SetState(DeviceState.Error);
            });
            Command_TestPowerMeter_Low = new RelayCommand<object>((obj) =>
            {
                PMDisplay?.SetState(DeviceState.Connecting);
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
                {
                    PMDisplay?.SetState(DeviceState.Error);
                    return;
                }
                PMDisplay?.SetState(DeviceState.Transporting);
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
                PMDisplay?.SetState(DeviceState.Connected);
            });
            Command_TestPowerMeter_High = new RelayCommand<object>((obj) =>
            {
                DevStateMap.TryGetValue("電表", out DeviceDisplay? PMDisplay);
                Instruction ins1 = (new(2, "檢查電表為高量程", Order.WaitModbus, [(ushort)0x36, (ushort)1, 5000])
                {
                    OnStart = (Ins) =>
                    {
                        PMDisplay?.SetState(DeviceState.Connecting);
                        SysLog.Add(LogLevel.Info, "檢查電表為高量程:(0x36) == 1");
                    },
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
                {
                    PMDisplay?.SetState(DeviceState.Error);
                    return;
                }
                PMDisplay?.SetState(DeviceState.Transporting);
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
                PMDisplay?.SetState(DeviceState.Connected);
            });
            Command_TestBurn = new RelayCommand<object>((obj) =>
            {
                Instruction ins =(new(6, "燒錄", Order.Burn, SelectedBurnType) {
                });
                Task.Run(() => ins.Execute());
            });
            InitializeCommands_G51();
            InitializeCommands_ZBRT();
            Command_TestN9000B = new RelayCommand<object>(async (obj) => await PrepareRF());
            Command_ReadN9000B = new RelayCommand<object>(async (obj) => await ReadRFValue());
        }
        private static async Task<bool> PrepareRF()
        {
            DeviceDisplay? RFDisplay = null;
            ///*RST 重置資料 //提前做
            ///*RCL 1 命令Recall File 1
            ///:INIT:CONT OFF 停用量測
            try
            {
                Global.MMain?.DevStateMap.TryGetValue("頻譜儀", out RFDisplay);
                RFDisplay?.SetState(DeviceState.Connecting);
                if (!Global.TcpCommand?.IsConnected ?? true)
                {
                    bool isC = await Global.LinkSignalAnalyzer();
                    if (!isC)
                    {
                        RFDisplay?.SetState(DeviceState.Error);
                        return false;
                    }
                    RFDisplay?.SetState(DeviceState.Connected);
                }
                RFDisplay?.SetState(DeviceState.Transporting);
                await Global.TcpCommand!.SendAndReceiveTokenAsync("*RST\r\n", "SCPI");
                SysLog.Add(LogLevel.Info, "頻譜儀重置");
                //Thread.Sleep(1000);
                await Global.TcpCommand.SendAndReceiveTokenAsync("*RCL 1\r\n", "SCPI");
                SysLog.Add(LogLevel.Info, "頻譜儀切換程序(Recall):1");

                await Global.TcpCommand.SendAndReceiveTokenAsync(":INIT:CONT OFF\r\n", "SCPI");
                Thread.Sleep(500);
                SysLog.Add(LogLevel.Warning, "頻譜儀準備完成"); 
                RFDisplay?.SetState(DeviceState.Connected);
                return true;
            }
            catch (Exception ex)
            {
                SysLog.Add(LogLevel.Error, $"頻譜儀通訊異常:{ex.Message}");
                RFDisplay?.SetState(DeviceState.Error);
                return false;
            }
        }
        private static async Task<double> ReadRFValue()
        {
            ///:ABORt 中斷下一次輪詢
            ///:FETC:BPOW? 做完後
            DeviceDisplay? RFDisplay = null;
            try
            {
                
                Global.MMain?.DevStateMap.TryGetValue("頻譜儀", out RFDisplay);
                if (!Global.TcpCommand?.IsConnected ?? true)
                {
                    RFDisplay?.SetState(DeviceState.Connecting);
                    bool isC = await Global.LinkSignalAnalyzer();
                    if (!isC)
                    {
                        RFDisplay?.SetState(DeviceState.Error);
                        return double.NaN;
                    }
                    RFDisplay?.SetState(DeviceState.Connected);
                }
                RFDisplay?.SetState(DeviceState.Transporting);
                await Global.TcpCommand!.ClearReadBuffer(Global.CtsTCP.Token);
                string[] ret1 = await Global.TcpCommand.SendAndReceiveTokenAsync(":ABOR\r\n", "SCPI", Global.CtsTCP.Token);
                SysLog.Add(LogLevel.Info, "讀取開始");
                string[] ret2 = await Global.TcpCommand.SendAndReceiveTokenAsync(":FETC:BPOW?\r\n", "SCPI", Global.CtsTCP.Token);
                if(ret2.Length >= 2)
                {
                    string[] data = ret2[1].Split(',');
                    if(data.Length >=3)
                    {
                        SysLog.Add(LogLevel.Info, $"頻譜儀回應: 輸出{data[2].ToDouble():F3} dBm");
                        RFDisplay?.SetState(DeviceState.Connected);
                        return data[2].ToDouble();
                    }
                }
                else SysLog.Add(LogLevel.Error, $"頻譜儀回應: 無檢測數據");

                RFDisplay?.SetState(DeviceState.Connected);
                return double.NaN;
            }
            catch(Exception ex)
            {
                SysLog.Add(LogLevel.Error, $"頻譜儀通訊異常:{ex.Message}");

                RFDisplay?.SetState(DeviceState.Error);
                return double.NaN;
            }
            
        }
        private void AddSigCount(int ID,string Memory, short Count, Action? ExtAction = null, int timeOut = 5000)
        {
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
                        Thread.Sleep(300);
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
                CurrentStepID = Math.Max(++CurrentStepID, 0);
                if (CurrentStepID <= StepsData.Count)
                {
                    MapSteps.TryGetValue(CurrentStepID, out step);
                    step?.SetStart();
                }
            });
        }

        internal void ResetSteps(int defaultStepID = 0, bool setStart = false)
        {
            DispMain?.Invoke(() =>
            {
                CurrentStepID = defaultStepID;
                foreach (var step in StepsData)
                    step.Reset();
                if (setStart)
                {
                    MapSteps.TryGetValue(CurrentStepID, out var step);
                    step?.SetStart();
                }
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
        public double? TestAntenna { get; set; }
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

    public enum DeviceState
    {
        DisConnect,
        Connecting,
        Connected,
        Transporting,
        Error
    }
    [AddINotifyPropertyChangedInterface]
    public class DeviceDisplay(string Title)
    {
        public string Title { get; set; } = Title;
        public SolidColorBrush BrushState { get; set; } = Brushes.Transparent;
        public DeviceState State { get; set; } = DeviceState.DisConnect;
        public Visibility VisbReconnect { get; set; } = Visibility.Collapsed;
        public ICommand? CommandReconnect { get; set; }

        public void SetState(DeviceState state)
        {
            Model_Main.DispMain?.Invoke(() =>
            {
                State = state;
                BrushState = (State) switch
                {
                    DeviceState.DisConnect => Brushes.Transparent,
                    DeviceState.Connecting => Brushes.Blue,
                    DeviceState.Connected => Brushes.LightGreen,
                    DeviceState.Transporting => Brushes.Green,
                    _ => Brushes.Red
                };
                if (State == DeviceState.Error)
                    VisbReconnect = Visibility.Visible;
                else
                    VisbReconnect = Visibility.Collapsed;
            });
        }
    }
}
