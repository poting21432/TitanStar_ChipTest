using Support;
using Support.Data;
using Support.Logger;
using Support.Wpf.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection.Metadata;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WpfApp_TitanStar_TestPlatform;

namespace WpfApp_TitanStar_TestPlatform
{
    public partial class Model_Main
    {
        static readonly Lazy<string> G51_v3_pos = new(() => "G51_Supply_3V+".GetPLCMem()); //M3008
        static readonly Lazy<string> G51_v3_neg = new(() =>"G51_Supply_3V-".GetPLCMem()); //M3009
        static readonly Lazy<string> G51_v5_pos = new(() =>"G51_Supply_5V+".GetPLCMem()) ; //M3003
        static readonly Lazy<string> G51_v5_neg = new(() =>"G51_Supply_5V-".GetPLCMem()); //M3004
        static readonly Lazy<string> G51_v_low  = new(() =>"G51_Supply_2.4V".GetPLCMem()); //M3011
        static readonly Lazy<string> G51_v3_prb = new(() => "G51_Supply_3V+Probe".GetPLCMem()); //M3010
        public ProcedureState G51BurnState { get; set; } = new("G51燒錄程序");
        public ProcedureState G51TestState { get; set; } = new("G51測試程序");
        
        internal static readonly string[] StrSteps_G51 = [
            "等待燒錄到位" ,"燒錄中", "燒錄完成復歸",
            "等待測試到位", "等待開關汽缸到位", "3V導通 LED閃爍檢測",
            "DIO探針LED檢測", "指撥1 - LED檢測", "指撥2 - LED檢測",
            "蓋開 - LED檢測", "5V,mA 電表測試", "磁簧汽缸 - LED檢測", "2.4V LED閃爍檢測",
            "測試開關 - LED檢測", "頻譜儀天線強度測試","3V,uA 電表測試", "完成並記錄資訊"
        ];
        #region G51 Related Commands
        public ICommand? Command_G51_3VON { get; set; }
        public ICommand? Command_G51_5VON { get; set; }
        public ICommand? Command_G51_PrbON { get; set; }
        public ICommand? Command_G51_LowV { get; set; }
        public ICommand? Command_G51_OFF { get; set; }

        private void InitializeCommands_G51()
        {
            Command_G51_3VON = new RelayCommand<object>((obj) =>
            {
                if (!Global.IsInitialized) return;
                Global.PLC.WriteRandomData([G51_v5_pos.Value, G51_v5_neg.Value, G51_v_low.Value, G51_v3_prb.Value], [0, 0, 0, 0]);
                Thread.Sleep(300);
                Global.PLC.WriteRandomData([G51_v3_pos.Value, G51_v3_neg.Value], [1, 1]);
                G51_Supply = "3V";
                SysLog.Add(LogLevel.Warning, $"手動切換電壓(G51):{G51_Supply}");
            });
            Command_G51_5VON = new RelayCommand<object>((obj) =>
            {
                if (!Global.IsInitialized) return;
                Global.PLC.WriteRandomData([G51_v3_prb.Value, G51_v_low.Value], [0, 0]);
                Thread.Sleep(300);
                Global.PLC.WriteRandomData([G51_v3_pos.Value, G51_v3_neg.Value, G51_v5_pos.Value, G51_v5_neg.Value], [1, 1, 1, 1]);
                G51_Supply = "5V";
                SysLog.Add(LogLevel.Warning, $"手動切換電壓(G51):{G51_Supply}");
            });
            Command_G51_PrbON = new RelayCommand<object>((obj) =>
            {
                if (!Global.IsInitialized) return;
                Global.PLC.WriteRandomData([G51_v5_pos.Value, G51_v5_neg.Value, G51_v_low.Value], [0, 0, 0]);
                Thread.Sleep(300);
                Global.PLC.WriteRandomData([G51_v3_pos.Value, G51_v3_neg.Value, G51_v3_prb.Value], [1, 1, 1]);
                Thread.Sleep(100);
                Global.PLC.WriteRandomData([G51_v3_pos.Value], [0]);
                G51_Supply = "探針";
                SysLog.Add(LogLevel.Warning, $"手動切換電壓(G51):{G51_Supply}");
            });
            Command_G51_LowV = new RelayCommand<object>((obj) =>
            {
                if (!Global.IsInitialized) return;
                Global.PLC.WriteRandomData([G51_v5_pos.Value, G51_v5_neg.Value, G51_v_low.Value], [0, 0, 0]);
                Thread.Sleep(300);
                Global.PLC.WriteRandomData([G51_v3_pos.Value, G51_v3_neg.Value, G51_v3_prb.Value], [1, 1, 1]);
                Thread.Sleep(100);
                Global.PLC.WriteRandomData([G51_v3_pos.Value], [0]);
                Thread.Sleep(100);
                Global.PLC.WriteRandomData([G51_v_low.Value], [1]);
                G51_Supply = "2.4V";
                SysLog.Add(LogLevel.Warning, $"手動切換電壓(G51):{G51_Supply}");
            });

            Command_G51_OFF = new RelayCommand<object>((obj) =>
            {
                if (!Global.IsInitialized) return;
                Global.PLC.WriteRandomData([G51_v3_pos.Value, G51_v3_neg.Value, G51_v5_pos.Value,
                                            G51_v5_neg.Value, G51_v_low.Value, G51_v3_prb.Value],
                                           [0, 0, 0, 0, 0, 0]);
                G51_Supply = "OFF";
                if(!(obj is bool isLog && !isLog))
                    SysLog.Add(LogLevel.Warning, $"手動切換電壓(G51):{G51_Supply}");
            });
        }
        #endregion
        #region G51 Test Sequence
        public void ProcedureTest_G51()
        {
            ProcedureState PState = G51TestState;
            if (PState.IsBusy)
            {
                SysLog.Add(LogLevel.Warning, $"{PState.Title}正在執行，先終止後重啟");
                return;
            }
            PState.SetStart();
            #region G51 Instructions
            Instructions.Clear();
            string memReady = "G51_Signal_Ready".GetPLCMem();
            string sen_cyUD_DN = "G51_Sensor_CyUD_DN".GetPLCMem();//M4001
            string sen_cyUD_UP = "G51_Sensor_CyUD_UP".GetPLCMem();//M4000
            string cyl_UD_UP = "G51_Cylinder_UD_UP".GetPLCMem();//M3012
            string cyl_UD_DN = "G51_Cylinder_UD_DN".GetPLCMem();//M3013
            string v3_pos = "G51_Supply_3V+".GetPLCMem(); //M3008
            string v3_neg = "G51_Supply_3V-".GetPLCMem(); //M3009
            string v5_pos = "G51_Supply_5V+".GetPLCMem(); //M3003
            string v5_neg = "G51_Supply_5V-".GetPLCMem(); //M3004
            string v_low = "G51_Supply_2.4V".GetPLCMem(); //M3011
            string v3_prb = "G51_Supply_3V+Probe".GetPLCMem(); //M3010

            string photoresistor = "G51_Sensor_Photoresistor".GetPLCMem();//M4003
            string cyl_switchTest = "G51_Cylinder_SwitchTest".GetPLCMem();//M3006
            string pin_DIO = "G51_Pin_DIO".GetPLCMem();//M3000
            string pin_switch1 = "G51_Pin_Switch1".GetPLCMem();//M3001
            string pin_switch2 = "G51_Pin_Switch2".GetPLCMem();//M3002
            string cyl_cover = "G51_Cylinder_Cover".GetPLCMem(); //M3005
            string cyl_reed = "G51_Cylinder_Reed".GetPLCMem(); //M3014
            string sen_reed = "G51_Sensor_CyReed".GetPLCMem(); //M4002

            string mem_result = "G51_Signal_Result".GetPLCMem();//M4400
            Instruction ins_pho_check4 = new(38, $"閃爍檢測4次", Order.PLCSignalCount, [Global.PLC, photoresistor, (short)4, 15000])
            {
                OnStart = (Ins) =>
                {
                    Ins.ExcResult = ExcResult.NotSupport; // 強制重設
                    SysLog.Add(LogLevel.Info, $"閃爍檢測 4次: {photoresistor} ^v 1");
                },
                OnEnd = (Ins) =>
                {
                    Ins.ExcResult = ExcResult.Success;
                    if (Ins.ExcResult != ExcResult.Success)
                        SysLog.Add(LogLevel.Error, "閃爍檢測計數錯誤");
                    else Thread.Sleep(300);
                }
            };
            Instruction ins_pho_check1 = new(38, $"閃爍檢測1次", Order.PLCSignalCount, [Global.PLC, photoresistor, (short)1, 5000])
            {
                OnStart = (Ins) =>
                {
                    Ins.ExcResult = ExcResult.NotSupport; // 強制重設
                    SysLog.Add(LogLevel.Info, $"閃爍檢測 1次: {photoresistor} ^v 1");
                },
                OnEnd = (Ins) =>
                {
                    Ins.ExcResult = ExcResult.Success;
                    if (Ins.ExcResult != ExcResult.Success)
                        SysLog.Add(LogLevel.Error, "閃爍檢測計數錯誤");
                    else Thread.Sleep(300);
                }
            };

            //*
            Instructions.Add(new(1, "工件放置確認", Order.WaitPLCSiganl, [Global.PLC, memReady, (short)1, 0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"等待工件放置完畢 {memReady} == 1"),
                OnEnd = (Ins) => SysLog.Add(LogLevel.Info, "確認G51工件到位")
            });//*/
            Instructions.Add(new(1, "斷電確認", Order.Custom)
            {
                OnStart = (Ins) =>
                {
                    SysLog.Add(LogLevel.Info, $"電壓初始化...");
                    Global.PLC.WriteRandomData(
                        [v3_pos, v3_neg, v3_prb, v5_neg, v5_neg, v_low],
                        [0, 0, 0, 0, 0, 0]);
                    DispMain?.Invoke(() => G51_Supply = "OFF");
                },
            });
            Instructions.Add(new(2, "汽缸上升程序", Order.WaitPLCSiganl, [Global.PLC, sen_cyUD_DN, (short)1, 2000])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"檢查升降汽缸:下定位 {sen_cyUD_DN} == 1"),
                OnEnd = (Ins) => {
                    if (Ins.ExcResult == ExcResult.Success)
                    {
                        SysLog.Add(LogLevel.Info, "確認升降汽缸在下定位");
                        Thread.Sleep(500);
                        SysLog.Add(LogLevel.Info, $"升降汽缸上升 {cyl_UD_UP} -> 1");
                        Global.PLC.WriteOneData(cyl_UD_UP, 1);
                        Thread.Sleep(1000);
                    }
                    else
                    {
                        SysLog.Add(LogLevel.Error, $"升降汽缸不在下定位 {sen_cyUD_DN} == 0");
                        Ins.ExcResult = ExcResult.Error;
                        return;
                    }
                    SysLog.Add(LogLevel.Info, $"等待升降汽缸上檢知 {sen_cyUD_UP} == 1");
                    DateTime stT = DateTime.Now; bool err = false;
                    while (Global.PLC.ReadOneData(sen_cyUD_UP).ReturnValue == 0)
                    {
                        if ((DateTime.Now - stT).TotalSeconds > 10)
                        {
                            err = true;
                            break;
                        }
                        Thread.Sleep(300);
                    }
                    if (err)
                    {
                        SysLog.Add(LogLevel.Error, "升降汽缸作動異常，上定位到位確認超時");
                        Ins.ExcResult = ExcResult.Error;
                    }
                    else
                    {
                        SysLog.Add(LogLevel.Info, $"確認汽缸已在上定位 {sen_cyUD_UP} == 1");
                        Global.PLC.WriteOneData(cyl_UD_UP, 0);
                        SysLog.Add(LogLevel.Info, $"升降汽缸上升復歸 {cyl_UD_UP} -> 0");
                    }

                }
            });
            Instructions.Add(new(12, "切電表至高量程", Order.SendModbus, [(ushort)0x001F, (ushort)2])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, "設定電表至高量程:(0x001F)->2")
            });
            Instructions.Add(new(12, "檢查電表為高量程", Order.WaitModbus, [(ushort)0x36, (ushort)1, 5000])
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

            Instructions.Add(new(13, $"測試開關汽缸上升{cyl_switchTest} -> 1", Order.SendPLCSignal, [Global.PLC, cyl_switchTest, (short)1])
            {
                OnStart = (Ins) => {
                    NextStep();//->"等待電表汽缸上升"
                    SysLog.Add(LogLevel.Info, $"測試開關汽缸上升{cyl_switchTest} -> 1");
                },
                OnEnd = (Ins) => Thread.Sleep(1000)
            });
            Instructions.Add(new(14, "導通3V", Order.Custom)
            {
                OnStart = (Ins) =>
                {
                    NextStep(); //->"3V導通 LED閃爍檢測"
                    SysLog.Add(LogLevel.Info, $"導通3V {v3_pos}->1 {v3_neg}->1");
                    CreatePhoTask(ins_pho_check4);
                    Global.PLC.WriteRandomData([v3_pos, v3_neg], [1, 1]);
                    DispMain?.Invoke(() => G51_Supply = "3V");
                },
                OnEnd = (Ins) => Ins.ExcResult = WaitPhoCheck("3V測試導通", Ins, ins_pho_check4,
                         () => CurrentProduct!.OnCheck = "V",
                         () => CurrentProduct!.OnCheck = "X")
            });
            
            Instructions.Add(new(16, $"測試開關汽缸下降 {cyl_switchTest} -> 0", Order.SendPLCSignal, [Global.PLC, cyl_switchTest, (short)0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"測試開關汽缸下降 {cyl_switchTest} -> 0"),
                OnEnd = (Ins) => Thread.Sleep(500)
            });
            Instructions.Add(new(17, $"IO點位測試導通 {pin_DIO} -> 1", Order.SendPLCSignal, [Global.PLC, pin_DIO, (short)1])
            {
                OnStart = (Ins) => {
                    NextStep();//-> "DIO探針LED檢測
                    CreatePhoTask(ins_pho_check1);
                    SysLog.Add(LogLevel.Info, $"IO點位測試導通 {pin_DIO} -> 1");
                },
                OnEnd = (Ins) => Ins.ExcResult = WaitPhoCheck("DIO斷開", Ins, ins_pho_check1,
                        null,() => CurrentProduct!.DIOCheck = "X")
            });
            Instructions.Add(new(19, $"IO點位測試關閉 {pin_DIO} -> 0", Order.SendPLCSignal, [Global.PLC, pin_DIO, (short)0])
            {
                OnStart = (Ins) =>
                {
                    CreatePhoTask(ins_pho_check1);
                    SysLog.Add(LogLevel.Info, $"IO點位測試關閉 {pin_DIO} -> 0");
                },
                OnEnd = (Ins) => Ins.ExcResult = WaitPhoCheck("DIO斷開", Ins, ins_pho_check1,
                        () => CurrentProduct!.DIOCheck = "V",
                        () => CurrentProduct!.DIOCheck = "X")
            });
            Instructions.Add(new(21, $"指撥開關-1導通 {pin_switch1} -> 1", Order.SendPLCSignal, [Global.PLC, pin_switch1, (short)1])
            {
                OnStart = (Ins) =>
                {
                    NextStep(); //-> "指撥1 - LED檢測"
                    CreatePhoTask(ins_pho_check1);
                    SysLog.Add(LogLevel.Info, $"指撥開關-1導通 {pin_switch1} -> 1");
                },
                OnEnd = (Ins) => Ins.ExcResult = WaitPhoCheck("指撥-1導通", Ins, ins_pho_check1,
                        null, () => CurrentProduct!.Switch1Check = "X")
            });
            Instructions.Add(new(23, $"指撥開關-1斷開 {pin_switch1} -> 0", Order.SendPLCSignal, [Global.PLC, pin_switch1, (short)0])
            {
                OnStart = (Ins) =>
                {
                    CreatePhoTask(ins_pho_check1);
                    SysLog.Add(LogLevel.Info, $"指撥開關-1斷開 {pin_switch1} -> 0");
                },
                OnEnd = (Ins) => Ins.ExcResult = WaitPhoCheck("指撥-1斷開", Ins, ins_pho_check1,
                        () => CurrentProduct!.Switch1Check = "V",
                        () => CurrentProduct!.Switch1Check = "X")
            });
            Instructions.Add(new(25, $"指撥開關-2導通 {pin_switch2}-> 1", Order.SendPLCSignal, [Global.PLC, pin_switch2, (short)1])
            {
                OnStart = (Ins) =>
                {
                    NextStep(); //-> "指撥2 - LED檢測"
                    CreatePhoTask(ins_pho_check1);
                    SysLog.Add(LogLevel.Info, $"指撥開關-2導通 {pin_switch2}-> 1");
                },
                OnEnd = (Ins) => Ins.ExcResult = WaitPhoCheck("指撥-2導通", Ins, ins_pho_check1,
                        null, () => CurrentProduct!.Switch2Check = "X")
            });
            Instructions.Add(new(27, $"指撥開關-2斷開 {pin_switch2} -> 0", Order.SendPLCSignal, [Global.PLC, pin_switch2, (short)0])
            {
                OnStart = (Ins) => {
                    CreatePhoTask(ins_pho_check1);
                    SysLog.Add(LogLevel.Info, $"指撥開關-2斷開 {pin_switch2} -> 0");
                },
                OnEnd = (Ins) => Ins.ExcResult = WaitPhoCheck("指撥-2斷開", Ins, ins_pho_check1,
                        () => CurrentProduct!.Switch2Check = "V",
                        () => CurrentProduct!.Switch2Check = "X")
            });
            Instructions.Add(new(29, $"蓋開升降汽缸下降 {cyl_cover}-> 1", Order.SendPLCSignal, [Global.PLC, cyl_cover, (short)1])
            {
                OnStart = (Ins) =>
                {
                    NextStep(); //-> "蓋開 - LED檢測"
                    CreatePhoTask(ins_pho_check1);
                    SysLog.Add(LogLevel.Info, $"蓋開升降汽缸下降 {cyl_cover}-> 1");
                },
                OnEnd = (Ins) => Ins.ExcResult = WaitPhoCheck("蓋開下降", Ins, ins_pho_check1, 
                null, () => CurrentProduct!.CoverCheck = "X")
            });

            Instructions.Add(new(31, $"蓋開汽缸上升 {cyl_cover} -> 0", Order.SendPLCSignal, [Global.PLC, cyl_cover, (short)0])
            {
                OnStart = (Ins) => {
                    CreatePhoTask(ins_pho_check1);
                    SysLog.Add(LogLevel.Info, $"蓋開汽缸上升 {cyl_cover} -> 0");
                },
                OnEnd = (Ins) =>
                    Ins.ExcResult = WaitPhoCheck("蓋開汽缸上升", Ins, ins_pho_check1,
                        () => CurrentProduct!.CoverCheck = "V",
                        () => CurrentProduct!.CoverCheck = "X")
            });
            Instructions.Add(new(33, "檢查電表為高量程", Order.WaitModbus, [(ushort)0x36, (ushort)1, 5000])
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
            Instructions.Add(new(33, "導通5V", Order.Custom)
            {
                OnStart = (Ins) =>
                {
                    CreatePhoTask(ins_pho_check1);
                    SysLog.Add(LogLevel.Info, $"導通5V {v5_pos}->1 {v5_neg}->1");
                    Global.PLC.WriteRandomData([v5_pos, v5_neg], [1, 1]);
                    DispMain?.Invoke(() => G51_Supply = "5V");
                },
                OnEnd = (Ins) => {
                    NextStep();
                    Ins.ExcResult = WaitPhoCheck("導通5V", Ins, ins_pho_check1);
                }
            });

            Instructions.Add(new(34, "讀電表值(高量程)", Order.ReadModbusFloat, [(ushort)0x0030])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, "讀取高量程電表數值:(0x0030)"),
                OnEnd = (Ins) =>
                {
                    float? v = Ins.Result as float?;
                    if (!v.HasValue)
                        Ins.ExcResult = ExcResult.Error;
                    else if (Math.Abs(v.Value) > 2.0)
                    {
                        SysLog.Add(LogLevel.Warning, $"電表數值過高(Abs(電壓)< 2.0):{Ins.Result}mA");
                        Ins.ExcResult = ExcResult.Error;
                    }
                    if (Ins.ExcResult == ExcResult.Success)
                    {
                        SysLog.Add(LogLevel.Info, $"讀取電表數值(高量程):{Ins.Result}mA");
                        DispMain?.Invoke(() => CurrentProduct!.Test5VmA = v);
                    }
                    else
                        SysLog.Add(LogLevel.Error, "電表數值異常");
                }
            });
            Instruction ins_initRF = new(43, $"頻譜儀初始化", Order.Custom)
            {
                OnStart = async (Ins) => {
                    bool isP = await PrepareRF();
                    Ins.ExcResult = isP ? ExcResult.Success : ExcResult.Error;
                },
            };
            Instructions.Add(new(33, "斷開5V", Order.Custom)
            {
                OnStart = (Ins) =>
                {
                    SysLog.Add(LogLevel.Info, $"斷開5V {v5_pos}->0 {v5_neg}->0");
                    Global.PLC.WriteRandomData([v5_pos, v5_neg], [0, 0]);
                    Ins.ExcResult = ExcResult.Success;
                    Task.Run(() => ins_initRF.Execute());
                    DispMain?.Invoke(() => G51_Supply = "3V");
                    Thread.Sleep(500);
                }
            });
            
            Instructions.Add(new(36, $"磁簧感應汽缸下降 {cyl_reed} -> 1", Order.SendPLCSignal, [Global.PLC, cyl_reed, (short)1])
            {
                OnStart = (Ins) => {
                    NextStep();//->"磁簧汽缸 - LED檢測"
                    CreatePhoTask(ins_pho_check1);
                    SysLog.Add(LogLevel.Info, $"磁簧感應汽缸下降 {cyl_reed} -> 1");
                },
            });
            Instructions.Add(new(37, "確認磁簧感應汽缸下降", Order.WaitPLCSiganl, [Global.PLC, sen_reed, (short)1, 5000])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"等待磁簧感應汽缸下降 {sen_reed} -> 1"),
                OnEnd = (Ins) => Ins.ExcResult = WaitPhoCheck("磁簧感應下降", Ins, ins_pho_check1, null,
                                                             () => CurrentProduct!.ReedCheck = "X")
            });

            Instructions.Add(new(39, $"磁簧感應汽缸上升 {cyl_reed} -> 0", Order.SendPLCSignal, [Global.PLC, cyl_reed, (short)0])
            {
                OnStart = (Ins) => {
                    CreatePhoTask(ins_pho_check1);
                    SysLog.Add(LogLevel.Info, $"磁簧感應汽缸上升 {cyl_reed} -> 0");
                },
            });

            Instructions.Add(new(40, $"等待磁簧感應汽缸上升 {sen_reed} -> 0", Order.WaitPLCSiganl, [Global.PLC, sen_reed, (short)0, 5000])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"等待磁簧感應汽缸上升 {sen_reed} -> 0"),
                OnEnd = (Ins) => Ins.ExcResult = WaitPhoCheck("磁簧感應上升", Ins, ins_pho_check1,
                                                             () => CurrentProduct!.ReedCheck = "V",
                                                             () => CurrentProduct!.ReedCheck = "X")
            });

            Instructions.Add(new(42, $"3V+導通(探針) {v3_prb}-> 1", Order.SendPLCSignal, [Global.PLC, v3_prb, (short)1])
            {
                OnStart = (Ins) => {
                    SysLog.Add(LogLevel.Info, $"3V+導通(探針) {v3_prb}-> 1");
                },
                OnEnd = (Ins) => Thread.Sleep(200)
            });

            Instructions.Add(new(43, $"3V+斷開(電流表) {v3_pos} -> 0", Order.SendPLCSignal, [Global.PLC, v3_pos, (short)0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"3V+斷開(電流表) {v3_pos} -> 0"),
                OnEnd = (Ins) =>
                {
                    Ins.ExcResult = ExcResult.Success;
                    DispMain?.Invoke(() => G51_Supply = "探針");
                }
            });
            Instructions.Add(new(44, $"2.4V導通 {v_low} -> 1", Order.SendPLCSignal, [Global.PLC, v_low, (short)1])
            {
                OnStart = (Ins) =>
                {
                    Thread.Sleep(300);
                    NextStep(); //->"2.4V LED閃爍檢測"
                    SysLog.Add(LogLevel.Info, $"2.4V導通 {v_low} -> 1");
                    Ins.ExcResult = ExcResult.Success;
                    DispMain?.Invoke(() => G51_Supply = "2.4V");
                }
            });
            ///NOTED 長亮檢查
            Instructions.Add(new(45, "長亮檢查", Order.Custom)
            {
                OnStart = (Ins) =>
                {
                    SysLog.Add(LogLevel.Info, "長亮檢查");
                    DateTime t_start = DateTime.Now;
                    while (Global.PLC.ReadOneData(photoresistor).ReturnValue == 0)
                    {
                        if ((DateTime.Now - t_start).TotalSeconds > 5.0)
                        {
                            Ins.ExcResult = ExcResult.TimeOut;
                            return;
                        }
                        Thread.Sleep(100);
                    }
                    Thread.Sleep(1000);
                    if (Global.PLC.ReadOneData(photoresistor).ReturnValue == 1)
                    {
                        DispMain?.Invoke(() => CurrentProduct!.LowVCheck = "V");
                        Ins.ExcResult = ExcResult.Success;
                        return;
                    }
                }
            });

            Instructions.Add(new(46, $"2.4V斷開 {v_low} -> 0", Order.SendPLCSignal, [Global.PLC, v_low, (short)0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"2.4V斷開 {v_low} -> 0"),
                OnEnd = (Ins) => DispMain?.Invoke(() => G51_Supply = "探針")
            });

            ///NOTED 頻譜儀檢查
            Instructions.Add(new(47, "測試開關汽缸上升", Order.SendPLCSignal, [Global.PLC, cyl_switchTest, (short)1])
            {
                OnStart = (Ins) =>
                {
                    NextStep(); //->"測試開關 - LED檢測"
                    SysLog.Add(LogLevel.Info, $"測試開關汽缸上升 {cyl_switchTest} -> 1");
                }
            });

            Instructions.Add(new(48, "等待亮燈", Order.WaitPLCSiganl, [Global.PLC, photoresistor, (short)1, 2000])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, "等待光敏檢知"),
                OnEnd = (Ins) => {
                    SysLog.Add(LogLevel.Info, "確認光敏檢知");
                    Ins.ExcResult = ExcResult.Success;
                },
            });

            Instructions.Add(new(49, "測試開關汽缸下降", Order.SendPLCSignal, [Global.PLC, cyl_switchTest, (short)0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"測試開關汽缸下降 {cyl_switchTest}-> 0"),
            });
            AddSigCount(48, photoresistor, 1, 
                ()=> DispMain?.Invoke(() => CurrentProduct!.OnOffCheck = "V"));

            Instructions.Add(new(50, "頻譜儀天線強度測試", Order.Custom)
            {
                OnStart = async (Ins) =>
                {
                    NextStep();  //"頻譜儀天線強度測試"
                    double rf_value = await ReadRFValue();
                    DispMain?.Invoke(() => CurrentProduct!.TestAntenna = rf_value);
                    Ins.ExcResult = double.IsNaN(rf_value) ? ExcResult.Error : ExcResult.Success;
                },
            });

            Instructions.Add(new(33, "斷開3V(探針)", Order.Custom)
            {
                OnStart = (Ins) =>
                {
                    SysLog.Add(LogLevel.Info, $"斷開3V {v3_prb}->0 {v3_neg}->0");
                    Global.PLC.WriteRandomData([v3_prb, v3_neg], [0, 0]);
                    Thread.Sleep(300);
                    Ins.ExcResult = ExcResult.Success;
                    DispMain?.Invoke(() => G51_Supply = "OFF");
                }
            });

            Instructions.Add(new(8, "切電表至低量程", Order.SendModbus, [(ushort)0x001F, (ushort)1])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, "設定電表至低量程:(0x001F)->1")
            });
            Instructions.Add(new(8, "檢查電表為低量程", Order.WaitModbus, [(ushort)0x36, (ushort)0, 5000])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, "檢查電表為低量程:(0x36) == 0"),
                OnEnd = (Ins) =>
                {
                    if (Ins.ExcResult == ExcResult.Success)
                        SysLog.Add(LogLevel.Info, "已確認電表: 低量程");
                    else
                        SysLog.Add(LogLevel.Error, "電表量程檢查失敗");
                }
            });

            Instructions.Add(new(9, "導通3V", Order.Custom)
            {
                OnStart = (Ins) =>
                {
                    SysLog.Add(LogLevel.Info, $"導通3V {v3_pos}->1 {v3_neg}->1");
                    Global.PLC.WriteRandomData([v3_pos, v3_neg], [1, 1]);
                    DispMain?.Invoke(() => G51_Supply = "3V");
                },
                OnEnd = (Ins) => {
                    Thread.Sleep(100);
                    NextStep(); //-> "3V,uA 電表測試"
                    Ins.ExcResult = ExcResult.Success;
                }
            });

            Instructions.Add(new(10, "讀電表值(低量程)", Order.WaitModbusFloat, [(ushort)0x0032, (float)1.0f, (float)8.0f, 15000])
            {
                OnStart = (Ins) => {
                    SysLog.Add(LogLevel.Info, "等待低量程電表(0x0032)數值(1uA~8uA)");
                },
                OnEnd = (Ins) =>
                {
                    float result = (Ins.Result == null) ? float.NaN : (float)Ins.Result;
                    if (Ins.ExcResult == ExcResult.Success)
                    {
                        SysLog.Add(LogLevel.Info, $"讀取電表數值(低量程):{result:F2}uA");
                        DispMain?.Invoke(() => CurrentProduct!.Test3VuA = result);
                    }
                    else if (Ins.ExcResult == ExcResult.Error)
                        SysLog.Add(LogLevel.Info, $"電表讀取錯誤(低量程)");
                    else if (Ins.ExcResult == ExcResult.TimeOut)
                    {
                        SysLog.Add(LogLevel.Info, $"電表數值異常(低量程):{result:F2}uA");
                        DispMain?.Invoke(() => CurrentProduct!.Test3VuA = result);
                    }
                    else SysLog.Add(LogLevel.Info, $"電表異常:未定義錯誤");
                }
            });
            Instructions.Add(new(9, "斷開3V", Order.Custom)
            {
                OnStart = (Ins) =>
                {
                    SysLog.Add(LogLevel.Info, $"斷開3V {v3_pos}->0 {v3_neg}->0");
                    Global.PLC.WriteRandomData([v3_pos, v3_neg], [0, 0]);
                    Ins.ExcResult = ExcResult.Success;
                    DispMain?.Invoke(() => G51_Supply = "OFF");
                }
            });

            Instructions.Add(new(52, "測試完畢", Order.SendPLCSignal, [Global.PLC, mem_result, (short)1])
            {
                OnEnd = (Ins) =>
                {
                    NextStep();
                    SysLog.Add(LogLevel.Success, $"產品作業完成 {mem_result} -> 1");
                    //Thread.Sleep(2000);
                },
            });
            //M3013 -> 1 升降汽缸下降
            //M4001 -> 1 確認下定位
            //M3013 -> 0 復歸
            Instructions.Add(new(51, "升降汽缸下降程序", Order.WaitPLCSiganl, [Global.PLC, sen_cyUD_UP, (short)1, 2000])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"檢查升降汽缸:上定位  {sen_cyUD_UP} == 1"),
                OnEnd = (Ins) => {
                    if (Ins.ExcResult == ExcResult.Success)
                    {
                        SysLog.Add(LogLevel.Info, "確認升降汽缸在上定位");
                        Thread.Sleep(500);
                        SysLog.Add(LogLevel.Info, $"升降汽缸下降 {cyl_UD_DN} -> 1");
                        Global.PLC.WriteOneData(cyl_UD_DN, 1);
                        Thread.Sleep(1000);
                    }
                    else
                    {
                        SysLog.Add(LogLevel.Error, $"升降汽缸不在上定位 {sen_cyUD_UP} == 0");
                        Ins.ExcResult = ExcResult.Error;
                        return;
                    }
                    SysLog.Add(LogLevel.Info, $"等待升降汽缸下檢知 {sen_cyUD_DN} == 1");
                    DateTime stT = DateTime.Now; bool err = false;
                    while (Global.PLC.ReadOneData(sen_cyUD_DN).ReturnValue == 0)
                    {
                        if ((DateTime.Now - stT).TotalSeconds > 10)
                        {
                            err = true;
                            break;
                        }
                        Thread.Sleep(500);
                    }
                    if (err)
                    {
                        SysLog.Add(LogLevel.Error, "升降汽缸作動異常，下定位到位確認超時");
                        Ins.ExcResult = ExcResult.Error;
                    }
                    else
                    {
                        SysLog.Add(LogLevel.Info, $"確認汽缸已在下定位 {sen_cyUD_DN} == 1");
                        Global.PLC.WriteOneData(cyl_UD_DN, 0);
                        SysLog.Add(LogLevel.Info, $"升降汽缸下降復歸 {cyl_UD_DN} -> 0");
                    }
                    NextStep();
                }
            });
            #endregion
            Task.Run(() =>
            {
                if(CurrentStepID != 4)
                {
                    if (CurrentProduct == null)
                        DispMain?.Invoke(() => {
                            CurrentProduct = new() { TimeStart = DateTime.Now };
                            ProductRecords.Add(CurrentProduct);
                        });
                    ResetSteps(4, setStart: true);
                }
                PState.SignalNext = false;
                if (PState.IsModeStep)
                    SysLog.Add(LogLevel.Warning, "步進模式");

                foreach (Instruction ins in Instructions)
                {
                    if (PState.IsCompleted) //錯誤發生提前結束
                        return;

                    bool ex = "測試流程".TryCatch(() =>
                    {
                        if (PState.IsModeStep)
                        {
                            SysLog.Add(LogLevel.Warning, $"{ins.Title}:步進等待...");
                            while (!PState.SignalNext &&
                                   !PState.IsReseting && !PState.IsStop)
                                Thread.Sleep(100);
                        }
                        if (PState.IsReseting || PState.IsStop)
                        {
                            PState.SetEnd(ExcResult.Abort);
                            ErrorStep();
                            return;
                        }
                        int id = ins.ID ?? -1;
                        string title = ins.Title;
                        ins?.Execute();
                        PState.SignalNext = false;
                        if (!PState.IsBypassErr && ins != null &&
                            ins.ExcResult != ExcResult.Success)
                        {
                            PState.SetEnd(ins.ExcResult);
                            ErrorStep();
                            G51TestNGRelease();
                            return;
                        }
                    });
                    if (!ex)
                    {
                        PState.SetEnd(ExcResult.Error);
                        ErrorStep();
                        G51TestNGRelease();
                        return;
                    }
                }
                if (CurrentProduct != null)
                    DispMain?.Invoke(() => CurrentProduct.TimeEnd = DateTime.Now);
                //Normal
                PState.SetEnd(ExcResult.Success);
                CurrentProduct = null;
            });
        }
        public void ProcedureG51ResetTest()
        {
            ProcedureState PState = G51TestState;
            if (PState.IsReseting)
            {
                SysLog.Add(LogLevel.Warning, $"{PState.Title}正在復歸");
                return;
            }
            PState.IsReseting = true;
            string sen_cyUD_DN = "G51_Sensor_CyUD_DN".GetPLCMem();//M4001
            string sen_cyUD_UP = "G51_Sensor_CyUD_UP".GetPLCMem();//M4000
            string cyl_UD_UP = "G51_Cylinder_UD_UP".GetPLCMem();//M3012
            string cyl_UD_DN = "G51_Cylinder_UD_DN".GetPLCMem();//M3013
            string cyl_switchTest = "G51_Cylinder_SwitchTest".GetPLCMem();//M3006
            string pin_DIO = "G51_Pin_DIO".GetPLCMem();//M3000
            string pin_switch1 = "G51_Pin_Switch1".GetPLCMem();//M3001
            string pin_switch2 = "G51_Pin_Switch2".GetPLCMem();//M3002
            string cyl_cover = "G51_Cylinder_Cover".GetPLCMem(); //M3005
            string cyl_reed = "G51_Cylinder_Reed".GetPLCMem(); //M3014

            "終止復歸".TryCatch(() =>
            {
                SysLog.Add(LogLevel.Warning, $"{PState.Title}:等待結束...");
                while (PState.IsBusy)
                    Thread.Sleep(1000);
                SysLog.Add(LogLevel.Info, $"{PState.Title}:確認終止，復歸中...");
                SysLog.Add(LogLevel.Warning, $"{PState.Title}:電壓源復歸...");
                //電路
                bool isLog = false;
                Command_G51_OFF!.Execute(isLog);
                Thread.Sleep(500);
                SysLog.Add(LogLevel.Warning, $"{PState.Title}:電壓源復歸完成");
                SysLog.Add(LogLevel.Warning, $"{PState.Title}:IO復歸...");
                //汽缸
                Global.PLC.WriteOneData(cyl_reed, 0);
                Global.PLC.WriteOneData(cyl_switchTest, 0);
                Global.PLC.WriteOneData(cyl_cover, 0);
                Thread.Sleep(500);
                //開關
                Global.PLC.WriteRandomData([pin_DIO, pin_switch1, pin_switch2], [0, 0, 0]);
                Thread.Sleep(200);
                SysLog.Add(LogLevel.Warning, $"{PState.Title}:IO復歸完成");

                if (Global.PLC.ReadOneData(sen_cyUD_UP).ReturnValue != 0)
                {
                    SysLog.Add(LogLevel.Warning, $"{PState.Title}:確認汽缸在上定位");
                    Global.PLC.WriteRandomData([cyl_UD_UP, cyl_UD_DN], [0, 0]);
                    Thread.Sleep(500);
                    SysLog.Add(LogLevel.Warning, $"{PState.Title}:升降汽缸下降復歸...");
                    Global.PLC.WriteOneData(cyl_UD_DN, 1);
                    Thread.Sleep(500);
                    while (Global.PLC.ReadOneData(sen_cyUD_DN).ReturnValue == 0)
                        Thread.Sleep(500);
                    Global.PLC.WriteRandomData([cyl_UD_UP, cyl_UD_DN], [0, 0]);
                }
                DispMain?.Invoke(() =>
                {
                    PState.BrushState = Brushes.Transparent;
                });
            }, () => PState.IsReseting = false);
            SysLog.Add(LogLevel.Warning, $"{PState.Title}:確認升降汽缸在下定位");
            SysLog.Add(LogLevel.Info, $"{PState.Title}:復歸完成");
        }
        private void G51TestNGRelease()
        {
            const short ng_value = 2;
            string mem_result = "G51_Signal_Result".GetPLCMem();//M4400
            SysLog.Add(LogLevel.Warning, $"{G51TestState.Title}:設定NG復歸...");
            Global.PLC.WriteOneData(mem_result, ng_value);
            ProcedureG51ResetTest(); 
            if (CurrentProduct != null)
                DispMain?.Invoke(() => CurrentProduct.TimeEnd = DateTime.Now);
            CurrentProduct = null;
        }

        private static void CreatePhoTask(Instruction ins_pho_check, int delay_ms=200)
        {
            Task.Run(() => ins_pho_check.Execute());
            Thread.Sleep(delay_ms);
        }
        private static ExcResult WaitPhoCheck(string Title,Instruction parent_ins,Instruction ins_pho_check,
            Action? OnSuccessDispInvoke = null, Action? OnErrorDispInvoke = null)
        {
            while (ins_pho_check.ExcResult != ExcResult.Success)
            {
                if (ins_pho_check.ExcResult == ExcResult.TimeOut)
                {
                    ins_pho_check.ExcResult = ExcResult.Error;
                    break;
                }
                Thread.Sleep(200);
            }
            if (ins_pho_check.ExcResult != ExcResult.Success)
            {
                SysLog.Add(LogLevel.Error, $"{Title}光敏檢知異常");
                if (OnSuccessDispInvoke != null)
                    DispMain?.Invoke(OnErrorDispInvoke);
            }
            else
            {
                SysLog.Add(LogLevel.Info, $"{Title}光敏檢知確認");
                if(OnSuccessDispInvoke != null)
                    DispMain?.Invoke(OnSuccessDispInvoke);
            }
            return ins_pho_check.ExcResult;
        }
        #endregion
        #region G51 Burn Sequence
        public void ProcedureG51ResetBurn()
        {
            string sen_cyUD_DN = "G51_Sensor_CyBUD_DN".GetPLCMem();//M4011
            string sen_cyUD_UP = "G51_Sensor_CyBUD_UP".GetPLCMem();//M4010
            string cyl_UD_UP = "G51_Cylinder_BUD_UP".GetPLCMem();//M3020
            string cyl_UD_DN = "G51_Cylinder_BUD_DN".GetPLCMem();//M3021

            ProcedureState PState = G51BurnState;
            if (PState.IsReseting)
            {
                SysLog.Add(LogLevel.Warning, $"{PState.Title}正在復歸");
                return;
            }
            PState.IsReseting = true;

            "終止復歸".TryCatch(() =>
            {
                if (PState.IsBusy && Global.ProcessBurn != null)
                    Global.ProcessBurn.Kill();
                SysLog.Add(LogLevel.Warning, $"{PState.Title}:等待結束...");
                while (PState.IsBusy)
                    Thread.Sleep(1000);
                SysLog.Add(LogLevel.Info, $"{PState.Title}:確認終止，復歸中...");
                if (Global.PLC.ReadOneData(sen_cyUD_UP).ReturnValue != 0)
                {
                    SysLog.Add(LogLevel.Warning, $"{PState.Title}:確認升降汽缸在上定位");
                    Global.PLC.WriteRandomData([cyl_UD_UP, cyl_UD_DN], [0, 0]);
                    Thread.Sleep(500);
                    SysLog.Add(LogLevel.Warning, $"{PState.Title}:升降汽缸下降復歸...");
                    Global.PLC.WriteOneData(cyl_UD_DN, 1);
                    Thread.Sleep(500);
                    while (Global.PLC.ReadOneData(sen_cyUD_DN).ReturnValue == 0)
                        Thread.Sleep(500);
                    Global.PLC.WriteRandomData([cyl_UD_UP, cyl_UD_DN], [0, 0]);
                }
            }, () => PState.IsReseting = false);
            SysLog.Add(LogLevel.Warning, $"{PState.Title}:確認升降汽缸在下定位");
            SysLog.Add(LogLevel.Info, $"{PState.Title}:復歸完成");
        }
        public void ProcedureBurn_G51()
        {
            ProcedureState PState = G51BurnState;
            if (PState.IsBusy)
            {
                SysLog.Add(LogLevel.Warning, $"{PState.Title}正在執行，先終止後重啟");
                return;
            }
            PState.SetStart();
            string sen_cyUD_DN = "G51_Sensor_CyBUD_DN".GetPLCMem();//M4011
            string sen_cyUD_UP = "G51_Sensor_CyBUD_UP".GetPLCMem();//M4010
            string cyl_UD_UP = "G51_Cylinder_BUD_UP".GetPLCMem();//M3020
            string cyl_UD_DN = "G51_Cylinder_BUD_DN".GetPLCMem();//M3021
            string memReady = "G51_Burn_Ready".GetPLCMem();
            string mem_burn_result = "G51_Burn_Result".GetPLCMem();//D3020
            #region G51 Burn Instructions
            InstructionsBurn.Clear();
            //*
            InstructionsBurn.Add(new(1, "工件放置確認", Order.WaitPLCSiganl, [Global.PLC, memReady, (short)1, 0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"等待燒錄工件放置完畢 {memReady} == 1")
            });//*/
            InstructionsBurn.Add(new(1, "汽缸上升程序", Order.WaitPLCSiganl, [Global.PLC, sen_cyUD_DN, (short)1, 2000])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"檢查燒錄升降汽缸:下定位  {sen_cyUD_DN} == 1"),
                OnEnd = (Ins) => {
                    if (Ins.ExcResult == ExcResult.Success)
                    {
                        SysLog.Add(LogLevel.Info, "確認燒錄升降汽缸在下定位");
                        Thread.Sleep(500);
                        SysLog.Add(LogLevel.Info, $"燒錄升降汽缸上升 {cyl_UD_UP} -> 1");
                        Global.PLC.WriteOneData(cyl_UD_UP, 1);
                        Thread.Sleep(1000);
                    }
                    else
                    {
                        SysLog.Add(LogLevel.Error, $"升降汽缸不在下定位 {sen_cyUD_DN} == 0");
                        Ins.ExcResult = ExcResult.Error;
                        return;
                    }
                    SysLog.Add(LogLevel.Info, $"等待升降汽缸上檢知 {sen_cyUD_UP} == 1");
                    DateTime stT = DateTime.Now; bool err = false;
                    while (Global.PLC.ReadOneData(sen_cyUD_UP).ReturnValue == 0)
                    {
                        if ((DateTime.Now - stT).TotalSeconds > 10)
                        {
                            err = true;
                            break;
                        }
                        Thread.Sleep(500);
                    }
                    if (err)
                    {
                        SysLog.Add(LogLevel.Error, "升降汽缸作動異常，上定位到位確認超時");
                        Ins.ExcResult = ExcResult.Error;
                    }
                    else
                    {
                        SysLog.Add(LogLevel.Info, $"確認汽缸已在上定位 {sen_cyUD_UP} == 1");
                        Global.PLC.WriteOneData(cyl_UD_UP, 0);
                        SysLog.Add(LogLevel.Info, $"升降汽缸上升復歸 {cyl_UD_UP} -> 0");
                    }

                }
            });
            InstructionsBurn.Add(new(2, "燒錄", Order.Burn, "PathBAT_G51")
            {
                OnStart= (Ins)=> DispMain?.Invoke(() => NextStep()),
                OnEnd = (Ins) =>
                {
                    DispMain?.Invoke(() => CurrentProduct!.BurnCheck = (Ins.ExcResult == ExcResult.Success) ? "V" : "X");
                    if (Ins.ExcResult != ExcResult.Success)
                    {
                        int reT = BurnRetryT;
                        Instruction insBurnRe = new(101, "燒錄", Order.Burn, "PathBAT_G51");
                        for (int i = 0; i < reT; i++)
                        {
                            SysLog.Add(LogLevel.Warning, $"{PState.Title}:燒錄重試開始...");
                            Thread.Sleep(500);
                            insBurnRe.Execute();
                            Ins.ExcResult = insBurnRe.ExcResult;
                            if (Ins.ExcResult == ExcResult.Success)
                                break;
                        }
                    }
                    if (Ins.ExcResult == ExcResult.Success)
                    {
                        Global.PLC.WriteOneData(mem_burn_result, 1);
                        SysLog.Add(LogLevel.Info, $"PLC確認燒錄OK {mem_burn_result} -> 1");
                    }
                    else
                    {
                        Global.PLC.WriteOneData(mem_burn_result, 2);
                        SysLog.Add(LogLevel.Warning, $"PLC確認燒錄NG {mem_burn_result} -> 2");
                    }
                    NextStep();
                }
            });
            InstructionsBurn.Add(new(3, "升降汽缸下降程序", Order.WaitPLCSiganl, [Global.PLC, sen_cyUD_UP, (short)1, 2000])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"檢查燒錄升降汽缸:上定位  {sen_cyUD_UP} == 1"),
                OnEnd = (Ins) => {
                    if (Ins.ExcResult == ExcResult.Success)
                    {
                        SysLog.Add(LogLevel.Info, "確認燒錄升降汽缸在上定位");
                        Thread.Sleep(500);
                        SysLog.Add(LogLevel.Info, $"燒錄升降汽缸下降 {cyl_UD_DN} -> 1");
                        Global.PLC.WriteOneData(cyl_UD_DN, 1);
                        Thread.Sleep(1000);
                    }
                    else
                    {
                        SysLog.Add(LogLevel.Error, $"燒錄升降汽缸不在上定位 {sen_cyUD_UP} == 0");
                        Ins.ExcResult = ExcResult.Error;
                        return;
                    }
                    SysLog.Add(LogLevel.Info, $"等待燒錄升降汽缸下檢知 {sen_cyUD_DN} == 1");
                    DateTime stT = DateTime.Now; bool err = false;
                    while (Global.PLC.ReadOneData(sen_cyUD_DN).ReturnValue == 0)
                    {
                        if ((DateTime.Now - stT).TotalSeconds > 5.0)
                        {
                            err = true;
                            break;
                        }
                        Thread.Sleep(500);
                    }
                    if (err)
                    {
                        SysLog.Add(LogLevel.Error, "燒錄升降汽缸作動異常，下定位到位確認超時");
                        Ins.ExcResult = ExcResult.Error;
                    }
                    else
                    {
                        SysLog.Add(LogLevel.Info, $"確認燒錄升降已在下定位 {sen_cyUD_DN} == 1");
                        Global.PLC.WriteOneData(cyl_UD_DN, 0);
                        SysLog.Add(LogLevel.Info, $"燒錄升降下降復歸 {cyl_UD_DN} -> 0");
                        NextStep();
                    }

                }
            });
            #endregion
            Task.Run(() =>
            {
                ResetSteps(1, setStart: true);
                DispMain?.Invoke(() =>
                {
                    CurrentProduct = new() { TimeStart = DateTime.Now };
                    ProductRecords.Add(CurrentProduct);
                });
                PState.SignalNext = false;
                if (PState.IsModeStep)
                    SysLog.Add(LogLevel.Warning, $"{PState.Title}:啟用步進模式");

                foreach (Instruction ins in InstructionsBurn)
                {
                    if (PState.IsCompleted)
                        return;
                    bool ex = "燒錄流程".TryCatch(() =>
                    {
                        if (PState.IsModeStep)
                        {
                            SysLog.Add(LogLevel.Warning, $"{ins.Title}:步進等待...");
                            while (!PState.SignalNext &&
                                   !PState.IsReseting && !PState.IsStop)
                                Thread.Sleep(100);
                        }
                        if (PState.IsReseting || PState.IsStop)
                        {
                            PState.SetEnd(ExcResult.Abort);
                            ErrorStep();
                            SysLog.Add(LogLevel.Warning, $"{PState.Title}:已終止，請指定復歸方式後排出");
                            return;
                        }
                        int id = ins.ID ?? -1;
                        string title = ins.Title;
                        ins?.Execute();
                        PState.SignalNext = false;
                        if (!PState.IsBypassErr && ins != null &&
                            ins.ExcResult != ExcResult.Success)
                        {
                            PState.SetEnd(ins.ExcResult);
                            ErrorStep();
                            G51BurnNGRelease();
                            return;
                        }
                    });
                    if(!ex)
                    {
                        PState.SetEnd(ExcResult.Error);
                        ErrorStep();
                        G51BurnNGRelease();
                        return;
                    }
                }
                //Normal
                PState.SetEnd(ExcResult.Success);
            });
        }

        private void G51BurnNGRelease()
        {
            const short ng_value = 2;
            string mem_burn_result = "G51_Burn_Result".GetPLCMem();//D3020
            SysLog.Add(LogLevel.Warning, $"{G51BurnState.Title}:設定NG復歸...");
            Global.PLC.WriteOneData(mem_burn_result, ng_value);
            if(CurrentProduct != null)
                DispMain?.Invoke(() => CurrentProduct.TimeEnd = DateTime.Now);
            ProcedureG51ResetBurn();
            CurrentProduct = null;
        }
        #endregion
    }
}
