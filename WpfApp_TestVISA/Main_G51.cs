using Support;
using Support.Data;
using Support.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace WpfApp_TestVISA
{
    public partial class Model_Main
    {
        public ProcedureState BurnState { get; set; } = new("燒錄程序");
        public ProcedureState TestState { get; set; } = new("測試程序");

        internal static readonly string[] StrSteps_G51 = [
            "等待產品到位", "等待開關汽缸到位", "3V導通 LED閃爍檢測",
            "DIO探針LED檢測", "指撥1 - LED檢測", "指撥2 - LED檢測",
            "蓋開 - LED檢測", "5V,mA 電表測試", "磁簧汽缸 - LED檢測", "2.4V LED閃爍檢測",
            "測試開關 - LED檢測", "頻譜儀天線強度測試","3V,uA 電表測試", "完成並記錄資訊"
        ];
        public void ProcedureMain_G51()
        {
            ProcedureState PState = TestState;
            if (PState.IsBusy)
            {
                SysLog.Add(LogLevel.Warning, $"{PState.Title}正在執行，先終止後重啟");
                return;
            }
            ResetSteps();
            Instructions.Clear();
            NextStep(); //->"等待到位"
            //*
            string memReady = "G51_Signal_Ready".GetPLCMem();
            Instructions.Add(new(1, "工件放置確認", Order.WaitPLCSiganl, [Global.PLC, memReady, (short)1, 0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"等待工件放置完畢 {memReady} == 1"),
                OnEnd = (Ins) => SysLog.Add(LogLevel.Info, "確認G51工件到位")
            });//*/

            DispMain?.Invoke(() =>
            {
                CurrentProduct = new() { TimeStart = DateTime.Now };
            });
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
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"確認汽缸在下定位 {sen_cyUD_DN} == 1"),
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
                OnEnd = (Ins) => Thread.Sleep(2000)
            });
            Instructions.Add(new(14, "導通3V", Order.Custom)
            {
                OnStart = (Ins) =>
                {
                    SysLog.Add(LogLevel.Info, $"導通3V {v3_pos}->1 {v3_neg}->1");
                    Global.PLC.WriteRandomData([v3_pos, v3_neg], [1, 1]);
                    DispMain?.Invoke(() => G51_Supply = "3V");
                },
                OnEnd = (Ins) => {
                    Thread.Sleep(100); 
                    Ins.ExcResult = ExcResult.Success;
                    NextStep(); //->"3V導通 LED閃爍檢測"
                }
            });
            AddSigCount(15, photoresistor, 4, () => DispMain?.Invoke(() => CurrentProduct!.OnCheck = "V"), 10000);
            Instructions.Add(new(16, $"測試開關汽缸下降 {cyl_switchTest} -> 0", Order.SendPLCSignal, [Global.PLC, cyl_switchTest, (short)0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"測試開關汽缸下降 {cyl_switchTest} -> 0"),
                OnEnd = (Ins) => Thread.Sleep(2000)
            });
            Instructions.Add(new(17, $"IO點位測試導通 {pin_DIO} -> 1", Order.SendPLCSignal, [Global.PLC, pin_DIO, (short)1])
            {
                OnStart = (Ins) => {
                    NextStep();//-> "DIO探針LED檢測
                    SysLog.Add(LogLevel.Info, $"IO點位測試導通 {pin_DIO} -> 1");
                }
            });
            AddSigCount(18, photoresistor, 1);
            Instructions.Add(new(19, $"IO點位測試關閉 {pin_DIO} -> 0", Order.SendPLCSignal, [Global.PLC, pin_DIO, (short)0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"IO點位測試關閉 {pin_DIO} -> 0"),
            });
            AddSigCount(20, photoresistor, 1, () => DispMain?.Invoke(() => CurrentProduct!.DIOCheck = "V"));
            Instructions.Add(new(21, $"指撥開關-1導通 {pin_switch1} -> 1", Order.SendPLCSignal, [Global.PLC, pin_switch1, (short)1])
            {
                OnStart = (Ins) =>
                {
                    NextStep(); //-> "指撥1 - LED檢測"
                    SysLog.Add(LogLevel.Info, $"指撥開關-1導通 {pin_switch1} -> 1");
                }
            });
            AddSigCount(22, photoresistor, 1);
            Instructions.Add(new(23, $"指撥開關-1斷開 {pin_switch1} -> 0", Order.SendPLCSignal, [Global.PLC, pin_switch1, (short)0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"指撥開關-1斷開 {pin_switch1} -> 0"),
            });
            AddSigCount(24, photoresistor, 1, () => DispMain?.Invoke(() => CurrentProduct!.Switch1Check = "V"));
            Instructions.Add(new(25, $"指撥開關-2導通 {pin_switch2}-> 1", Order.SendPLCSignal, [Global.PLC, pin_switch2, (short)1])
            {
                OnStart = (Ins) =>
                {
                    NextStep(); //-> "指撥2 - LED檢測"
                    SysLog.Add(LogLevel.Info, $"指撥開關-2導通 {pin_switch2}-> 1");
                }
            });
            AddSigCount(26, photoresistor, 1);
            Instructions.Add(new(27, $"指撥開關-2斷開 {pin_switch2} -> 0", Order.SendPLCSignal, [Global.PLC, pin_switch2, (short)0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"指撥開關-2斷開 {pin_switch2} -> 0"),
            });
            AddSigCount(28, photoresistor, 1, () => DispMain?.Invoke(() => CurrentProduct!.Switch2Check = "V"));
            Instructions.Add(new(29, $"蓋開升降汽缸下降 {cyl_cover}-> 1", Order.SendPLCSignal, [Global.PLC, cyl_cover, (short)1])
            {
                OnStart = (Ins) => {
                    NextStep(); //-> "蓋開 - LED檢測"
                    SysLog.Add(LogLevel.Info, $"蓋開升降汽缸下降 {cyl_cover}-> 1");
                },
            });
            AddSigCount(30, photoresistor, 1);
            Instructions.Add(new(31, $"蓋開升降汽缸上升 {cyl_cover} -> 0", Order.SendPLCSignal, [Global.PLC, cyl_cover, (short)0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"蓋開升降汽缸上升 {cyl_cover} -> 0"),
            });
            AddSigCount(32, photoresistor, 1, () => DispMain?.Invoke(() => CurrentProduct!.CoverCheck = "V"));
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
                    SysLog.Add(LogLevel.Info, $"導通5V {v5_pos}->1 {v5_neg}->1");
                    Global.PLC.WriteRandomData([v5_pos, v5_neg], [1, 1]);
                    DispMain?.Invoke(() => G51_Supply = "5V");
                },
                OnEnd = (Ins) => {
                    Thread.Sleep(100); 
                    Ins.ExcResult = ExcResult.Success;
                    NextStep();
                }
            });
            AddSigCount(33, photoresistor, 1);
            Instructions.Add(new(34, "讀電表值(高量程)", Order.ReadModbusFloat, [(ushort)0x0030])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, "讀取高量程電表數值:(0x0030)"),
                OnEnd = (Ins) =>
                {
                    float? v = Ins.Result as float?;
                    if (!v.HasValue)
                        Ins.ExcResult = ExcResult.Error;
                    if (v > 2.0)
                    {
                        SysLog.Add(LogLevel.Warning, $"電表數值過高(高量程 < 2.0):{Ins.Result}mA");
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
            Instructions.Add(new(33, "斷開5V", Order.Custom)
            {
                OnStart = (Ins) =>
                {
                    SysLog.Add(LogLevel.Info, $"斷開5V {v5_pos}->0 {v5_neg}->0");
                    Global.PLC.WriteRandomData([v5_pos, v5_neg], [0, 0]);
                    Ins.ExcResult = ExcResult.Success;
                    DispMain?.Invoke(() => G51_Supply = "3V");
                }
            });
            Instruction ins_check1 = new(38, $"閃爍檢測1次", Order.PLCSignalCount, [Global.PLC, photoresistor, (short)1, 5000])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"閃爍檢測 1次: {photoresistor} ^v 1"),
                OnEnd = (Ins) =>
                {
                    Ins.ExcResult = ExcResult.Success;
                    if (Ins.ExcResult != ExcResult.Success)
                        SysLog.Add(LogLevel.Error, "閃爍檢測計數錯誤");
                    else
                    {
                        SysLog.Add(LogLevel.Info, $"確認閃爍1次");
                        Thread.Sleep(2000);
                    }
                }
            };
            
            Instructions.Add(new(36, $"磁簧感應汽缸下降 {cyl_reed} -> 1", Order.SendPLCSignal, [Global.PLC, cyl_reed, (short)1])
            {
                OnStart = (Ins) => {
                    Task.Run(() => {
                        ins_check1.Execute();
                    });
                    NextStep();//->"磁簧汽缸 - LED檢測"
                    SysLog.Add(LogLevel.Info, $"磁簧感應汽缸下降 {cyl_reed} -> 1");
                },
            });
            Instructions.Add(new(37, "確認磁簧感應汽缸下降", Order.WaitPLCSiganl, [Global.PLC, sen_reed, (short)1, 5000])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"等待磁簧感應汽缸下降 {sen_reed} -> 1"),
                OnEnd = (Ins) =>
                {
                    while (ins_check1.ExcResult != ExcResult.Success)
                    {
                        if (ins_check1.ExcResult == ExcResult.TimeOut)
                        {
                            Ins.ExcResult = ExcResult.Error;
                            break;
                        }
                        Thread.Sleep(200);
                    }
                    if (Ins.ExcResult != ExcResult.Success)
                        SysLog.Add(LogLevel.Error, "磁簧感應汽缸下降異常");
                    else
                        SysLog.Add(LogLevel.Info, "確認磁簧感應汽缸下降");
                }
            });
            Instruction ins_check2 = new(41, $"閃爍檢測1次", Order.PLCSignalCount, [Global.PLC, photoresistor, (short)1, 5000])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"閃爍檢測 1次: {photoresistor} ^v 1"),
                OnEnd = (Ins) =>
                {
                    Ins.ExcResult = ExcResult.Success;
                    if (Ins.ExcResult != ExcResult.Success)
                        SysLog.Add(LogLevel.Error, "閃爍檢測計數錯誤");
                    else
                    {
                        SysLog.Add(LogLevel.Info, $"確認閃爍1次");
                        Thread.Sleep(2000);
                    }
                }
            };

            Instructions.Add(new(39, $"磁簧感應汽缸上升 {cyl_reed} -> 0", Order.SendPLCSignal, [Global.PLC, cyl_reed, (short)0])
            {
                OnStart = (Ins) => {
                    Task.Run(() => {
                        ins_check2.Execute();
                    });
                    SysLog.Add(LogLevel.Info, $"磁簧感應汽缸上升 {cyl_reed} -> 0");
                },
            });

            Instructions.Add(new(40, $"等待磁簧感應汽缸上升 {sen_reed} -> 0", Order.WaitPLCSiganl, [Global.PLC, sen_reed, (short)0, 5000])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"等待磁簧感應汽缸上升 {sen_reed} -> 0"),
                OnEnd = (Ins) =>
                {
                    
                    while (ins_check2.ExcResult != ExcResult.Success)
                    {
                        if (ins_check2.ExcResult == ExcResult.TimeOut)
                        {
                            Ins.ExcResult = ExcResult.Error;
                            break;
                        }
                        Thread.Sleep(200);
                    }
                    if (Ins.ExcResult != ExcResult.Success)
                        SysLog.Add(LogLevel.Error, "磁簧感應汽缸上升異常");
                    else
                        SysLog.Add(LogLevel.Info, "確認磁簧感應汽缸上升");
                }
            });
           
            Instructions.Add(new(42, $"3V+導通(探針) {v3_prb}-> 1", Order.SendPLCSignal, [Global.PLC, v3_prb, (short)1])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"3V+導通(探針) {v3_prb}-> 1"),
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
            Instruction ins_initRF = new(43, $"頻譜儀初始化", Order.Custom)
            {
                OnStart = async (Ins) => {
                    await Global.TcpCommand.SendAndReceiveSequenceAsync("*RST\r\n", 2);
                    SysLog.Add(LogLevel.Info, "頻譜儀重置");
                    Thread.Sleep(1000);
                    await Global.TcpCommand.SendAndReceiveSequenceAsync("*RCL 1\r\n", 2);
                    SysLog.Add(LogLevel.Info, "頻譜儀切換程序(Recall):1");
                    Thread.Sleep(500);
                    await Global.TcpCommand.SendAndReceiveSequenceAsync(":INIT:CONT OFF\r\n", 2);
                    SysLog.Add(LogLevel.Warning, "頻譜儀準備完成");
                    Ins.ExcResult = ExcResult.Success;
                },
            };
            Instructions.Add(new(44, $"2.4V導通 {v_low} -> 1", Order.SendPLCSignal, [Global.PLC, v_low, (short)1])
            {
                OnStart = (Ins) =>
                {
                    Task.Run(() => {
                        ins_initRF.Execute();
                    });
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
                    Thread.Sleep(2000);
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
                OnEnd = (Ins)=> DispMain?.Invoke(() => G51_Supply = "探針")
            });

            ///NOTED 頻譜儀檢查
            Instructions.Add(new(47, "測試開關汽缸上升", Order.SendPLCSignal, [Global.PLC, cyl_switchTest, (short)1])
            {
                OnStart = (Ins) =>
                {
                    NextStep(); //->"測試開關 - LED檢測"
                    SysLog.Add(LogLevel.Info, $"測試開關汽缸上升 {cyl_switchTest} -> 1");
                },
                OnEnd =(Ins) => Thread.Sleep(100)
            });

            Instructions.Add(new(48, "等待亮燈", Order.WaitPLCSiganl, [Global.PLC, photoresistor, (short)1, 2000])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, "等待光敏檢知"),
                OnEnd = (Ins) => {
                    SysLog.Add(LogLevel.Info, "確認光敏檢知");
                    Ins.ExcResult = ExcResult.Success;
                    Thread.Sleep(500);
                    },
            });

            Instructions.Add(new(49, "測試開關汽缸下降", Order.SendPLCSignal, [Global.PLC, cyl_switchTest, (short)0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"測試開關汽缸下降 {cyl_switchTest}-> 0"),
            });
            AddSigCount(48, photoresistor, 1);
            Instructions.Add(new(50, "頻譜儀天線強度測試", Order.Custom)
            {
                OnStart = async (Ins) =>
                {
                    NextStep();  //"頻譜儀天線強度測試"
                    Thread.Sleep(500);
                    await Global.TcpCommand.SendAndReceiveAsync(":ABOR\r\n");
                    SysLog.Add(LogLevel.Info, "讀取開始");

                    string[] ret = await Global.TcpCommand.SendAndReceiveSequenceAsync(":FETC:BPOW?\r\n", 3);
                    if (ret.Length >= 3)
                    {
                        string[] data = ret[2].Split(',');
                        double rf_out = data[2].ToDouble();
                        SysLog.Add(LogLevel.Info, $"頻譜儀回應: 輸出{rf_out:F3} dBm");
                        DispMain?.Invoke(() => CurrentProduct!.TestAntenna = $"{rf_out:F3}dBm");
                        Ins.ExcResult = ExcResult.Success;
                    }
                    else Ins.ExcResult = ExcResult.Error;
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

            Instructions.Add(new(10, "讀電表值(低量程)", Order.WaitModbusFloat, [(ushort)0x0032, (float) 1.0f, (float) 8.0f, 8000])
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

            string mem_result = "G51_Signal_Result".GetPLCMem();//M4400
            Instructions.Add(new(52, "測試完畢", Order.SendPLCSignal, [Global.PLC, mem_result, (short)1])
            {
                OnEnd = (Ins) =>
                {
                    NextStep();
                    SysLog.Add(LogLevel.Success, $"產品作業完成 {mem_result} -> 1");
                    Thread.Sleep(2000);
                },
            });
            //M3013 -> 1 升降汽缸下降
            //M4001 -> 1 確認下定位
            //M3013 -> 0 復歸
            Instructions.Add(new(51, "升降汽缸下降程序", Order.WaitPLCSiganl, [Global.PLC, sen_cyUD_UP, (short)1, 2000])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"確認汽缸在上定位 {sen_cyUD_UP} == 1"),
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
            Task.Run(() =>
            {
                PState.SignalNext = false;
                if (PState.IsModeStep)
                    SysLog.Add(LogLevel.Warning, "步進模式");
                PState.SetStart();

                foreach (Instruction ins in Instructions)
                {
                    if (PState.IsCompleted) //錯誤發生提前結束
                    {

                        return;
                    }
                     
                    "流程".TryCatch(() =>
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
                            PState.SetEnd(StateID: 3);
                            return;
                        }
                        int id = ins.ID ?? -1;
                        string title = ins.Title;
                        ins?.Execute();
                        PState.SignalNext = false;
                        if (ins != null && ins.ExcResult != ExcResult.Success)
                        {
                            PState.SetEnd(StateID: 2);
                            return;
                        }
                    }, () => PState.SetEnd(StateID: 2));
                }
                //Normal
                PState.SetEnd(StateID: 1);
            });
        }
        public void ProcedureBurn_G51()
        {
            ProcedureState PState = BurnState;
            if (PState.IsBusy)
            {
                SysLog.Add(LogLevel.Warning, $"{PState.Title}正在執行，先終止後重啟");
                return;
            }
            string sen_cyUD_DN = "G51_Sensor_CyBUD_DN".GetPLCMem();//M4011
            string sen_cyUD_UP = "G51_Sensor_CyBUD_UP".GetPLCMem();//M4010
            string cyl_UD_UP = "G51_Cylinder_BUD_UP".GetPLCMem();//M3020
            string cyl_UD_DN = "G51_Cylinder_BUD_DN".GetPLCMem();//M3021
            InstructionsBurn.Clear();
            //*
            string memReady = "G51_Burn_Ready".GetPLCMem();
            InstructionsBurn.Add(new(1, "工件放置確認", Order.WaitPLCSiganl, [Global.PLC, memReady, (short)1, 0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"等待燒錄工件放置完畢 {memReady} == 1"),
                OnEnd = (Ins) =>
                {
                }
            });//*/
            InstructionsBurn.Add(new(1, "汽缸上升程序", Order.WaitPLCSiganl, [Global.PLC, sen_cyUD_DN, (short)1, 2000])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"確認汽缸在下定位 {sen_cyUD_DN} == 1"),
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
            string mem_burn_result = "G51_Burn_Result".GetPLCMem();//D3020
            InstructionsBurn.Add(new(2, "燒錄", Order.Burn, "PathBAT_G51")
            {
                OnStart = (Ins) =>
                {
                    Thread.Sleep(1000);
                },
                OnEnd = (Ins) => DispMain?.Invoke(() =>
                {
                    //CurrentProduct!.BurnCheck = (Ins.ExcResult == ExcResult.Success) ? "V" : "X";
                    if (Ins.ExcResult != ExcResult.Success)
                    {
                        int reT = BurnRetryT;
                        Instruction insBurnRe = new(101, "燒錄", Order.Burn, "PathBAT_G51");
                        for (int i = 0; i < reT; i++)
                        {
                            SysLog.Add(LogLevel.Warning, "燒錄重試開始...");
                            Thread.Sleep(500);
                            insBurnRe.Execute();
                            Ins.ExcResult = insBurnRe.ExcResult;
                            if (Ins.ExcResult == ExcResult.Success)
                                break;
                        }
                    }
                    if(Ins.ExcResult == ExcResult.Success)
                    {
                        Global.PLC.WriteOneData(mem_burn_result, 1);
                        SysLog.Add(LogLevel.Info, $"PLC確認燒錄OK {mem_burn_result} -> 1");
                    }
                    else
                    {
                        Global.PLC.WriteOneData(mem_burn_result, 2);
                        SysLog.Add(LogLevel.Warning, $"PLC確認燒錄NG {mem_burn_result} -> 2");
                    }
                    Ins.ExcResult = ExcResult.Success;
                })
            });
            InstructionsBurn.Add(new(3, "升降汽缸下降程序", Order.WaitPLCSiganl, [Global.PLC, sen_cyUD_UP, (short)1, 2000])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"確認燒錄升降汽缸在上定位 {sen_cyUD_UP} == 1"),
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
                        if ((DateTime.Now - stT).TotalSeconds > 10)
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
                    }

                }
            });
            Task.Run(() =>
            {
                PState.SignalNext = false;
                if (PState.IsModeStep)
                    SysLog.Add(LogLevel.Warning, "步進模式");
                PState.SetStart();

                foreach (Instruction ins in InstructionsBurn)
                {
                    if (PState.IsCompleted)
                        return;
                    "燒錄流程".TryCatch(() =>
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
                            PState.SetEnd(StateID: 3);
                            return;
                        }
                        int id = ins.ID ?? -1;
                        string title = ins.Title;
                        ins?.Execute();
                        PState.SignalNext = false;
                        if (ins != null && ins.ExcResult != ExcResult.Success)
                        {
                            PState.SetEnd(StateID: 2);
                            return;
                        }
                    }, () => PState.SetEnd(StateID: 2));
                }
                //Normal
                PState.SetEnd(StateID: 1);
            });
        }
        public void ProcedureG51ResetTest()
        {
            ProcedureState PState = TestState;
            if (PState.IsReseting)
            {
                SysLog.Add(LogLevel.Warning, $"{PState}正在終止復歸");
                return;
            }
            PState.IsReseting = true;
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

            string cyl_switchTest = "G51_Cylinder_SwitchTest".GetPLCMem();//M3006
            string pin_DIO = "G51_Pin_DIO".GetPLCMem();//M3000
            string pin_switch1 = "G51_Pin_Switch1".GetPLCMem();//M3001
            string pin_switch2 = "G51_Pin_Switch2".GetPLCMem();//M3002
            string cyl_cover = "G51_Cylinder_Cover".GetPLCMem(); //M3005
            string cyl_reed = "G51_Cylinder_Reed".GetPLCMem(); //M3014

            "終止復歸".TryCatch(() =>
            {
                SysLog.Add(LogLevel.Warning, $"等待{PState.Title}結束...");
                while (PState.IsBusy)
                    Thread.Sleep(1000);
                SysLog.Add(LogLevel.Info, $"確認{PState.Title}終止，復歸中...");
                SysLog.Add(LogLevel.Warning, "電壓源復歸...");
                //電路
                Global.PLC.WriteRandomData([v_low ,v5_pos, v5_neg], [0, 0, 0]);
                Global.PLC.WriteRandomData([v3_pos, v3_neg, v3_prb], [0, 0, 0]);
                DispMain?.Invoke(() => G51_Supply = "OFF");
                Thread.Sleep(1000);
                SysLog.Add(LogLevel.Warning, "電壓源復歸完成");
                SysLog.Add(LogLevel.Warning, "IO復歸...");
                //汽缸
                Global.PLC.WriteOneData(cyl_reed, 0);
                Global.PLC.WriteOneData(cyl_switchTest, 0);
                Global.PLC.WriteOneData(cyl_cover, 0);
                Thread.Sleep(500);
                //開關
                Global.PLC.WriteRandomData([pin_DIO, pin_switch1, pin_switch2], [0, 0, 0]);
                Thread.Sleep(500);
                SysLog.Add(LogLevel.Warning, "IO復歸完成");

                if (Global.PLC.ReadOneData(sen_cyUD_UP).ReturnValue != 0)
                {
                    SysLog.Add(LogLevel.Warning, "升降汽缸復歸...");
                    SysLog.Add(LogLevel.Info, "確認升降汽缸在上定位");
                    Global.PLC.WriteRandomData([cyl_UD_UP,cyl_UD_DN], [0 ,0]);
                    Thread.Sleep(500);
                    SysLog.Add(LogLevel.Info, $"升降汽缸下降...");
                    Global.PLC.WriteOneData(cyl_UD_DN, 1);
                    Thread.Sleep(500);
                    while (Global.PLC.ReadOneData(sen_cyUD_DN).ReturnValue == 0)
                        Thread.Sleep(500);
                    SysLog.Add(LogLevel.Info, $"確認升降汽缸已在下定位");
                    Global.PLC.WriteRandomData([cyl_UD_UP, cyl_UD_DN], [0, 0]);
                    SysLog.Add(LogLevel.Info, "升降汽缸下降復歸完成");
                }
                DispMain?.Invoke(() =>
                {
                    PState.BrushState = Brushes.Transparent;
                });
            }, () => PState.IsReseting = false);
        }
        public void ProcedureG51ResetBurn()
        {
            string sen_cyUD_DN = "G51_Sensor_CyBUD_DN".GetPLCMem();//M4011
            string sen_cyUD_UP = "G51_Sensor_CyBUD_UP".GetPLCMem();//M4010
            string cyl_UD_UP = "G51_Cylinder_BUD_UP".GetPLCMem();//M3020
            string cyl_UD_DN = "G51_Cylinder_BUD_DN".GetPLCMem();//M3021

            ProcedureState PState = BurnState;
            if (PState.IsReseting)
            {
                SysLog.Add(LogLevel.Warning, $"{PState}正在終止復歸");
                return;
            }
            PState.IsReseting = true;

            "終止復歸".TryCatch(() =>
            {
                if (PState.IsBusy && Global.ProcessBurn != null)
                    Global.ProcessBurn.Kill();
                SysLog.Add(LogLevel.Warning, $"等待{PState.Title}結束...");
                while (PState.IsBusy)
                    Thread.Sleep(1000);
                SysLog.Add(LogLevel.Info, $"確認{PState.Title}終止，復歸中...");
                if (Global.PLC.ReadOneData(sen_cyUD_UP).ReturnValue != 0)
                {
                    SysLog.Add(LogLevel.Warning, "升降汽缸復歸...");
                    SysLog.Add(LogLevel.Info, "確認升降汽缸在上定位");
                    Global.PLC.WriteRandomData([cyl_UD_UP, cyl_UD_DN], [0, 0]);
                    Thread.Sleep(500);
                    SysLog.Add(LogLevel.Info, $"升降汽缸下降...");
                    Global.PLC.WriteOneData(cyl_UD_DN, 1);
                    Thread.Sleep(500);
                    while (Global.PLC.ReadOneData(sen_cyUD_DN).ReturnValue == 0)
                        Thread.Sleep(500);
                    SysLog.Add(LogLevel.Info, $"確認升降汽缸已在下定位");
                    Global.PLC.WriteRandomData([cyl_UD_UP, cyl_UD_DN], [0, 0]);
                    SysLog.Add(LogLevel.Info, "升降汽缸下降復歸完成");
                }
            }, () => PState.IsReseting = false);
        }
    }
}
