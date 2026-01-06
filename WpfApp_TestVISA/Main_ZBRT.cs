using PropertyChanged;
using Support;
using Support.Data;
using Support.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace WpfApp_TestVISA
{
    public partial class Model_Main
    {
        internal static readonly string[] StrSteps_ZBRT = [
            "等待產品到位","3V導通LED檢測", "蓋開LED檢測", "5V導通LED檢測",
            "低電壓LED檢測", "測試開關 - LED檢測", "頻譜儀天線強度測試", 
            "完成並記錄資訊"
        ];
        public void ProcedureTest_ZBRT()
        {
            ProcedureState PState = ZBRTTestState;
            if (PState.IsBusy)
            {
                SysLog.Add(LogLevel.Warning, $"{PState.Title}正在執行，先終止後重啟");
                return;
            }
            PState.SetStart();
            ResetSteps();
            Instructions.Clear();
            NextStep(); //->"等待探針到位"

            DispMain?.Invoke(() =>
            {
                CurrentProduct = new() { TimeStart = DateTime.Now };
            });

            string memReady = "ZBRT_Signal_Ready".GetPLCMem();
            Instructions.Add(new(1, "工件放置確認", Order.WaitPLCSiganl, [Global.PLC, memReady, (short)1, 0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"等待工件放置完畢 {memReady} == 1"),
                OnEnd = (Ins) =>
                {
                }
            });
            string sen_cyUD_UP = "ZBRT_Sensor_CyUD_UP".GetPLCMem();//M4050
            string sen_cyUD_DN = "ZBRT_Sensor_CyUD_DN".GetPLCMem();//M4051
            string cyl_UD_UP = "ZBRT_Cylinder_UD_UP".GetPLCMem();//M3059
            string cyl_UD_DN = "ZBRT_Cylinder_UD_DN".GetPLCMem();//M3060
            string mem_switch = "ZBRT_Cylinder_SwitchTest".GetPLCMem();//M3053
            string v5_pos = "ZBRT_Supply_5V+".GetPLCMem();//M3051
            string v5_neg = "ZBRT_Supply_5V-".GetPLCMem();//M3052
            string v3_pos = "ZBRT_Supply_3V+".GetPLCMem();//M3055
            string v3_neg = "ZBRT_Supply_3V-".GetPLCMem();//M3056
            string v_low = "ZBRT_Supply_2.4V".GetPLCMem();//M3058
            string photoresistor = "ZBRT_Sensor_Photoresistor".GetPLCMem();//M4052
            string cyl_cover = "ZBRT_Cylinder_Cover".GetPLCMem();//M3053
            Instructions.Add(new(1, "斷電確認", Order.Custom)
            {
                OnStart = (Ins) =>
                {
                    SysLog.Add(LogLevel.Info, $"電壓初始化...");
                    Global.PLC.WriteRandomData(
                        [v3_pos, v3_neg, v5_neg, v5_neg, v_low],
                        [0, 0, 0, 0, 0]);
                    DispMain?.Invoke(() => ZBRT_Supply = "OFF");
                },
            });

            Instructions.Add(new(2, "汽缸上升程序", Order.WaitPLCSiganl, [Global.PLC, sen_cyUD_DN, (short)1, 2000])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"檢查升降汽缸:下定位  {sen_cyUD_DN} == 1"),
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
                        SysLog.Add(LogLevel.Info, $"升降汽缸上升復歸 {cyl_UD_UP}-> 0");
                    }

                }
            });
            Instructions.Add(new(3, "登錄開關汽缸下降", Order.SendPLCSignal, [Global.PLC, mem_switch, (short)1])
            {
                OnStart = (Ins) => {
                    NextStep();
                    SysLog.Add(LogLevel.Info, $"登錄開關汽缸下降 {mem_switch} -> 1");
                },
                OnEnd = (Ins) => Thread.Sleep(2000)
            });
            Instructions.Add(new(33, "導通5V", Order.Custom)
            {
                OnStart = (Ins) =>
                {
                    NextStep(); //->"5V,mA 電表測試"
                    SysLog.Add(LogLevel.Info, $"導通5V {v5_pos}->1 {v5_neg}->1");
                    Global.PLC.WriteRandomData([v5_pos, v5_neg], [1, 1]);
                },
                OnEnd = (Ins) => {
                    Thread.Sleep(500);
                    Ins.ExcResult = ExcResult.Success;
                }
            });
            Instructions.Add(new(14, "導通3V", Order.Custom)
            {
                OnStart = (Ins) =>
                {
                    SysLog.Add(LogLevel.Info, $"導通3V {v3_pos}->1 {v3_neg}->1");
                    Global.PLC.WriteRandomData([v3_pos, v3_neg], [1, 1]);
                },
                OnEnd = (Ins) => {
                    Thread.Sleep(100);
                    Ins.ExcResult = ExcResult.Success;
                }
            });
            AddSigCount(15, photoresistor, 4, null, 10000);//, () => DispMain?.Invoke(() => CurrentProduct!.OnCheck = "V"));
            Instruction ins_check2 = new(41, $"閃爍檢測1次", Order.PLCSignalCount, [Global.PLC, photoresistor, (short)1, 5000])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"閃爍檢測 1次: {photoresistor} ^v 1"),
                OnEnd = (Ins) =>
                {
                    if (Ins.ExcResult != ExcResult.Success)
                        SysLog.Add(LogLevel.Error, "閃爍檢測計數錯誤");
                    else
                        SysLog.Add(LogLevel.Info, $"確認閃爍1次");
                }
            };
            Instructions.Add(new(16, "登錄開關汽缸上升", Order.SendPLCSignal, [Global.PLC, mem_switch, (short)0])
            {
                OnStart = (Ins) => {
                    Task.Run(() => {
                        ins_check2.Execute();
                    });
                    SysLog.Add(LogLevel.Info, $"登錄開關汽缸上升 {mem_switch} -> 0");
                },
                OnEnd =(Ins) =>
                {
                    while (ins_check2.ExcResult != ExcResult.Success)
                    {
                        if (ins_check2.ExcResult == ExcResult.TimeOut)
                        {
                            Ins.ExcResult = ExcResult.Error;
                            return;
                        }
                        Thread.Sleep(200);
                    }
                }
            });
            Instructions.Add(new(29, "蓋開升降汽缸下降", Order.SendPLCSignal, [Global.PLC, cyl_cover, (short)1])
            {
                OnStart = (Ins) => {
                    NextStep();
                    SysLog.Add(LogLevel.Info, $"蓋開升降汽缸下降 {cyl_cover} -> 1");
                },
            });
            AddSigCount(30, photoresistor, 1);
            Instructions.Add(new(31, "蓋開升降汽缸上升", Order.SendPLCSignal, [Global.PLC, cyl_cover, (short)0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"蓋開升降汽缸上升 {cyl_cover} -> 0"),
            });
            AddSigCount(32, photoresistor, 1);//, () => DispMain?.Invoke(() => CurrentProduct!.CoverCheck = "V"));

            Instructions.Add(new(33, "斷開5V", Order.Custom)
            {
                OnStart = (Ins) =>
                {
                    SysLog.Add(LogLevel.Info, $"斷開5V {v5_pos}->0 {v5_neg}->0");
                    Global.PLC.WriteRandomData([v5_pos, v5_neg], [0, 0]);
                    Ins.ExcResult = ExcResult.Success;
                }
            });
            AddSigCount(34, photoresistor, 1);//, () => DispMain?.Invoke(() => CurrentProduct!.ReedCheck = "V"));
            Instruction ins_initRF = new(43, $"頻譜儀初始化", Order.Custom)
            {
                OnStart = async (Ins) => {
                    bool isP = await PrepareRF();
                    Ins.ExcResult = isP ? ExcResult.Success : ExcResult.Error;
                },
            };

            Instructions.Add(new(44, "2.4V導通", Order.SendPLCSignal, [Global.PLC, v_low, (short)1])
            {
                OnStart = (Ins) =>
                {
                    Task.Run(() => {
                        ins_initRF.Execute();
                    });
                    NextStep(); //->"2.4V LED閃爍檢測"
                    SysLog.Add(LogLevel.Info, $"2.4V導通 {v_low} -> 1");
                }
            });

            Instructions.Add(new(44, "長亮檢查", Order.Custom)
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
                        Ins.ExcResult = ExcResult.Success;
                        return;
                    }
                }
            });
            
            Instructions.Add(new(46, "2.4V斷開", Order.SendPLCSignal, [Global.PLC, v_low, (short)0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"2.4V斷開 {v_low} -> 0"),
            });
            Instructions.Add(new(47, "測試開關汽缸下降", Order.SendPLCSignal, [Global.PLC, mem_switch, (short)1])
            {
                OnStart = (Ins) =>
                {
                    NextStep(); //->"測試開關LED檢測"
                    SysLog.Add(LogLevel.Info, $"測試開關汽缸下降 {mem_switch}-> 1");
                }
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
            Instructions.Add(new(49, "測試開關汽缸上升", Order.SendPLCSignal, [Global.PLC, mem_switch, (short)0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"測試開關汽缸上升 {mem_switch} -> 0")
            });
            AddSigCount(48, photoresistor, 1);//, () => DispMain?.Invoke(() => CurrentProduct!.OnOffCheck = "V"));
            Instructions.Add(new(50, "頻譜儀天線強度測試", Order.Custom)
            {
                OnStart = async (Ins) =>
                {
                    NextStep();  //"頻譜儀天線強度測試"
                    double value = await ReadRFValue();
                    Ins.ExcResult = double.IsNaN(value) ? ExcResult.Error : ExcResult.Success;
                },
            });
            string mem_result = "ZBRT_Signal_Result".GetPLCMem();//D3031
            Instructions.Add(new(52, "測試結果", Order.SendPLCSignal, [Global.PLC, mem_result, (short)1])
            {
                OnEnd = (Ins) =>
                {
                    NextStep();
                    SysLog.Add(LogLevel.Success, $"產品作業完成 {mem_result} -> 1");
                    Thread.Sleep(3000);
                },
            });
            Instructions.Add(new(14, "斷開3V+", Order.SendPLCSignal, [Global.PLC, v3_pos, (short)0])
            {
                OnStart = (Ins) => {
                    SysLog.Add(LogLevel.Info, $"斷開3V+ {v3_pos}->0");
                }
            });
            Instructions.Add(new(14, "斷開3V-", Order.SendPLCSignal, [Global.PLC, v3_neg, (short)0])
            {
                OnStart = (Ins) =>
                {
                    NextStep();//->"3V導通 LED閃爍檢測"
                    SysLog.Add(LogLevel.Info, $"斷開3V- {v3_neg}->0");
                },
                OnEnd = (Ins) => Thread.Sleep(1000)
            });

            Instructions.Add(new(51, "升降汽缸下降程序", Order.WaitPLCSiganl, [Global.PLC, sen_cyUD_UP, (short)1, 2000])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"檢查升降汽缸:上定位 {sen_cyUD_UP} == 1"),
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
                        SysLog.Add(LogLevel.Error, $"升降汽缸不在上定位 {sen_cyUD_UP}== 0");
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
                }
            });
            Task.Run(() =>
            {
                PState.SignalNext = false;
                if (PState.IsModeStep)
                    SysLog.Add(LogLevel.Warning, "步進模式");

                foreach (Instruction ins in Instructions)
                {
                    if (PState.IsCompleted)
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
                            return;
                        }
                        int id = ins.ID ?? -1;
                        string title = ins.Title;
                        ins?.Execute();
                        PState.SignalNext = false;
                        if (ins != null && ins.ExcResult != ExcResult.Success)
                        {
                            PState.SetEnd(ins.ExcResult);
                            return;
                        }
                    });
                    if (!ex)
                    {
                        PState.SetEnd(ExcResult.Error);
                        return;
                    }
                }
                //Normal
                PState.SetEnd(ExcResult.Success);
            });
        }
        public void ProcedureBurn_ZBRT()
        {
            ProcedureState PState = ZBRTBurnState;
            if (PState.IsBusy)
            {
                SysLog.Add(LogLevel.Warning, $"{PState.Title}正在執行，先終止後重啟");
                return;
            }
            PState.SetStart();
            string sen_cyUD_UP = "ZBRT_Sensor_CyBUD_UP".GetPLCMem();//M4050
            string sen_cyUD_DN = "ZBRT_Sensor_CyBUD_DN".GetPLCMem();//M4051
            string cyl_UD_UP = "ZBRT_Cylinder_BUD_UP".GetPLCMem();//M3059
            string cyl_UD_DN = "ZBRT_Cylinder_BUD_DN".GetPLCMem();//M3060
            InstructionsBurn.Clear();
            string memReady = "ZBRT_Burn_Ready".GetPLCMem();
            InstructionsBurn.Add(new(1, "工件放置確認", Order.WaitPLCSiganl, [Global.PLC, memReady, (short)1, 0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"等待燒錄工件放置完畢 {memReady} == 1"),
                OnEnd = (Ins) =>
                {
                }
            });

            InstructionsBurn.Add(new(1, "汽缸上升程序", Order.WaitPLCSiganl, [Global.PLC, sen_cyUD_DN, (short)1, 2000])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"檢查燒錄升降汽缸:下定位{sen_cyUD_DN} == 1"),
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
            string mem_burn_result = "ZBRT_Burn_Result".GetPLCMem();//D3021
            InstructionsBurn.Add(new(2, "燒錄", Order.Burn, "PathBAT_ZBRT")
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
                        Instruction insBurnRe = new(101, "燒錄", Order.Burn, "PathBAT_ZBRT");
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
                    Ins.ExcResult = ExcResult.Success;
                })
            });
            InstructionsBurn.Add(new(3, "升降汽缸下降程序", Order.WaitPLCSiganl, [Global.PLC, sen_cyUD_UP, (short)1, 2000])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"檢查燒錄升降汽缸:上定位 {sen_cyUD_UP} == 1"),
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
                    NextStep();
                }
            });
            Task.Run(() =>
            {
                PState.SignalNext = false;
                if (PState.IsModeStep)
                    SysLog.Add(LogLevel.Warning, "步進模式");

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
                            return;
                        }
                        int id = ins.ID ?? -1;
                        string title = ins.Title;
                        ins?.Execute();
                        PState.SignalNext = false;
                        if (ins != null && ins.ExcResult != ExcResult.Success)
                        {
                            PState.SetEnd(ins.ExcResult);
                            return;
                        }
                    });

                    if (!ex)
                    {
                        PState.SetEnd(ExcResult.Error);
                        return;
                    }
                }
                //Normal
                PState.SetEnd(ExcResult.Success);
            });
        }

        public void ProcedureZBRTResetTest()
        {
            ProcedureState PState = ZBRTTestState;
            if (PState.IsReseting)
            {
                SysLog.Add(LogLevel.Warning, $"{PState.Title}正在復歸");
                return;
            }
            PState.IsReseting = true;
            string sen_cyUD_UP = "ZBRT_Sensor_CyUD_UP".GetPLCMem();//M4050
            string sen_cyUD_DN = "ZBRT_Sensor_CyUD_DN".GetPLCMem();//M4051
            string cyl_UD_UP = "ZBRT_Cylinder_UD_UP".GetPLCMem();//M3059
            string cyl_UD_DN = "ZBRT_Cylinder_UD_DN".GetPLCMem();//M3060
            string cyl_switch = "ZBRT_Cylinder_SwitchTest".GetPLCMem();//M3053
            string v5_pos = "ZBRT_Supply_5V+".GetPLCMem();//M3051
            string v5_neg = "ZBRT_Supply_5V-".GetPLCMem();//M3052
            string v3_pos = "ZBRT_Supply_3V+".GetPLCMem();//M3055
            string v3_neg = "ZBRT_Supply_3V-".GetPLCMem();//M3056
            string v_low = "ZBRT_Supply_2.4V".GetPLCMem();//M3058
            string cyl_cover = "ZBRT_Cylinder_Cover".GetPLCMem();//M3053

            "終止復歸".TryCatch(() =>
            {
                SysLog.Add(LogLevel.Warning, $"{PState.Title}:等待結束...");
                while (PState.IsBusy)
                    Thread.Sleep(1000);
                SysLog.Add(LogLevel.Info, $"{PState.Title}:確認終止，復歸中...");
                SysLog.Add(LogLevel.Warning, $"{PState.Title}:電壓源復歸...");
                //電路
                Global.PLC.WriteRandomData([v_low, v5_pos, v5_neg], [0, 0, 0]);
                Global.PLC.WriteRandomData([v3_pos, v3_neg], [0, 0]);
                DispMain?.Invoke(() => ZBRT_Supply = "OFF");
                Thread.Sleep(1000);
                SysLog.Add(LogLevel.Warning, $"{PState.Title}:電壓源復歸完成");
                SysLog.Add(LogLevel.Warning, $"{PState.Title}:IO復歸...");
                //汽缸
                Global.PLC.WriteOneData(cyl_switch, 0);
                Global.PLC.WriteOneData(cyl_cover, 0);
                Thread.Sleep(500);
                SysLog.Add(LogLevel.Warning, $"{PState.Title}:IO復歸完成");

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
                DispMain?.Invoke(() =>
                {
                    PState.BrushState = Brushes.Transparent;
                });
            }, () => PState.IsReseting = false);
            SysLog.Add(LogLevel.Warning, $"{PState.Title}:確認升降汽缸在下定位");
            SysLog.Add(LogLevel.Info, $"{PState.Title}:復歸完成");
        }
        public void ProcedureZBRTResetBurn()
        {
            string sen_cyUD_UP = "ZBRT_Sensor_CyBUD_UP".GetPLCMem();//M4050
            string sen_cyUD_DN = "ZBRT_Sensor_CyBUD_DN".GetPLCMem();//M4051
            string cyl_UD_UP = "ZBRT_Cylinder_BUD_UP".GetPLCMem();//M3059
            string cyl_UD_DN = "ZBRT_Cylinder_BUD_DN".GetPLCMem();//M3060

            ProcedureState PState = ZBRTBurnState;
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
                SysLog.Add(LogLevel.Warning, $"{PState.Title}:確認終止，復歸中...");
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
            SysLog.Add(LogLevel.Warning, $"{PState.Title}:復歸完成");
        }
    }
}
