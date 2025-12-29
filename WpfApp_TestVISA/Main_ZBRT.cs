using PropertyChanged;
using Support;
using Support.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp_TestVISA
{
    public partial class Model_Main
    {
        internal static readonly string[] StrSteps_ZBRT = [
            "等待產品到位","3V導通LED檢測", "蓋開LED檢測", "5V導通LED檢測",
            "低電壓LED檢測", "測試開關 - LED檢測", "頻譜儀天線強度測試", 
            "完成並記錄資訊"
        ];
        public void ProcedureMain_ZBRT()
        {
            if (IsBusy)
            {
                SysLog.Add(LogLevel.Warning, "程序正在執行");
                return;
            }
            IsBusy = true;
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
            string sensor_cyUD_UP = "ZBRT_Sensor_CyUD_UP".GetPLCMem();//M4050
            string sensor_cyUD_DN = "ZBRT_Sensor_CyUD_DN".GetPLCMem();//M4051
            string cylinder_cyUD_UP = "ZBRT_Cylinder_UD_UP".GetPLCMem();//M3059
            string cylinder_cyUD_DN = "ZBRT_Cylinder_UD_DN".GetPLCMem();//M3060
            Instructions.Add(new(2, "汽缸上升程序", Order.WaitPLCSiganl, [Global.PLC, sensor_cyUD_DN, (short)1, 2000])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"確認升降汽缸在下定位 {sensor_cyUD_DN} == 1"),
                OnEnd = (Ins) => {
                    if (Ins.ExcResult == ExcResult.Success)
                    {
                        SysLog.Add(LogLevel.Info, "確認升降汽缸在下定位");
                        Thread.Sleep(500);
                        SysLog.Add(LogLevel.Info, $"升降汽缸上升 {cylinder_cyUD_UP} -> 1");
                        Global.PLC.WriteOneData(cylinder_cyUD_UP, 1);
                        Thread.Sleep(1000);
                    }
                    else
                    {
                        SysLog.Add(LogLevel.Error, $"升降汽缸不在下定位 {sensor_cyUD_DN} == 0");
                        Ins.ExcResult = ExcResult.Error;
                        return;
                    }
                    SysLog.Add(LogLevel.Info, $"等待升降汽缸上檢知 {sensor_cyUD_UP} == 1");
                    DateTime stT = DateTime.Now; bool err = false;
                    while (Global.PLC.ReadOneData(sensor_cyUD_UP).ReturnValue == 0)
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
                        SysLog.Add(LogLevel.Info, $"確認汽缸已在上定位 {sensor_cyUD_UP} == 1");
                        Global.PLC.WriteOneData(cylinder_cyUD_UP, 0);
                        SysLog.Add(LogLevel.Info, $"升降汽缸上升復歸 {cylinder_cyUD_UP}-> 0");
                    }

                }
            });
            string mem_switch = "ZBRT_Cylinder_SwitchTest".GetPLCMem();//M3053
            Instructions.Add(new(13, "登錄開關汽缸下降", Order.SendPLCSignal, [Global.PLC, mem_switch, (short)1])
            {
                OnStart = (Ins) => {
                    NextStep();
                    SysLog.Add(LogLevel.Info, $"登錄開關汽缸下降 {mem_switch} -> 1");
                },
                OnEnd = (Ins) => Thread.Sleep(2000)
            });
            string mem_5Vpos = "ZBRT_Supply_5V+".GetPLCMem();//M3051
            string mem_5Vneg = "ZBRT_Supply_5V-".GetPLCMem();//M3052
            Instructions.Add(new(33, "導通5V+", Order.SendPLCSignal, [Global.PLC, mem_5Vpos, (short)1])
            {
                OnStart = (Ins) =>
                {
                    NextStep();
                    SysLog.Add(LogLevel.Info, $"導通5V+ {mem_5Vpos}->1");
                },
                OnEnd = (Ins) => Thread.Sleep(100)
            });
            Instructions.Add(new(33, "導通5V-", Order.SendPLCSignal, [Global.PLC, mem_5Vneg, (short)1])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"導通5V- {mem_5Vneg}->1"),
                OnEnd = (Ins) => Thread.Sleep(100)
            });
            string mem_3Vpos = "ZBRT_Supply_3V+".GetPLCMem();//M3055
            Instructions.Add(new(14, "導通3V+", Order.SendPLCSignal, [Global.PLC, mem_3Vpos, (short)1])
            {
                OnStart = (Ins) =>  SysLog.Add(LogLevel.Info, $"導通3V+ {mem_3Vpos}->1"),
                OnEnd = (Ins) => Thread.Sleep(100)
            });
            string mem_3Vneg = "ZBRT_Supply_3V-".GetPLCMem();//M3056
            Instructions.Add(new(14, "導通3V-", Order.SendPLCSignal, [Global.PLC, mem_3Vneg, (short)1])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"導通3V- {mem_3Vneg}->1"),
                OnEnd = (Ins) => Thread.Sleep(100)
            });

            string photoresistor = "ZBRT_Sensor_Photoresistor".GetPLCMem();//M4052
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
            string mem_cover = "ZBRT_Cylinder_Cover".GetPLCMem();//M3053
            Instructions.Add(new(29, "蓋開升降汽缸下降", Order.SendPLCSignal, [Global.PLC, mem_cover, (short)1])
            {
                OnStart = (Ins) => {
                    NextStep();
                    SysLog.Add(LogLevel.Info, $"蓋開升降汽缸下降 {mem_cover} -> 1");
                },
            });
            AddSigCount(30, photoresistor, 1);
            Instructions.Add(new(31, "蓋開升降汽缸上升", Order.SendPLCSignal, [Global.PLC, mem_cover, (short)0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"蓋開升降汽缸上升 {mem_cover} -> 0"),
            });
            AddSigCount(32, photoresistor, 1);//, () => DispMain?.Invoke(() => CurrentProduct!.CoverCheck = "V"));
            
            Instructions.Add(new(35, "斷開5V+", Order.SendPLCSignal, [Global.PLC, mem_5Vpos, (short)0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"斷開5V+ {mem_5Vpos}->0"),
            });
            Instructions.Add(new(35, "斷開5V-", Order.SendPLCSignal, [Global.PLC, mem_5Vneg, (short)0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"斷開5V- {mem_5Vneg}->0"),
            });
            AddSigCount(34, photoresistor, 1);//, () => DispMain?.Invoke(() => CurrentProduct!.ReedCheck = "V"));
            
            string mem_LowV = "ZBRT_Supply_2.4V".GetPLCMem();//M3058
            Instructions.Add(new(44, "2.4V導通", Order.SendPLCSignal, [Global.PLC, mem_LowV, (short)1])
            {
                OnStart = (Ins) =>
                {
                    NextStep(); //->"2.4V LED閃爍檢測"
                    SysLog.Add(LogLevel.Info, $"2.4V導通 {mem_LowV} -> 1");
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
            
            Instructions.Add(new(46, "2.4V斷開", Order.SendPLCSignal, [Global.PLC, mem_LowV, (short)0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"2.4V斷開 {mem_LowV} -> 0"),
            });
            Instructions.Add(new(47, "測試開關汽缸下降", Order.SendPLCSignal, [Global.PLC, mem_switch, (short)1])
            {
                OnStart = (Ins) =>
                {
                    NextStep(); //->"測試開關LED檢測"
                    SysLog.Add(LogLevel.Info, $"測試開關汽缸下降 {mem_switch}-> 1");
                }
            });
            AddSigCount(48, photoresistor, 1);
            Instructions.Add(new(49, "測試開關汽缸上升", Order.SendPLCSignal, [Global.PLC, mem_switch, (short)0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"測試開關汽缸上升 {mem_switch} -> 0"),
            });
            AddSigCount(48, photoresistor, 1);//, () => DispMain?.Invoke(() => CurrentProduct!.OnOffCheck = "V"));
            Instructions.Add(new(50, "頻譜儀天線強度測試", Order.Custom)
            {
                OnStart = (Ins) =>
                {
                    NextStep();  //"頻譜儀天線強度測試"
                    //DispMain?.Invoke(() => CurrentProduct!.TestAntenna = "~");
                    SysLog.Add(LogLevel.Warning, "略過頻譜儀天線強度測試程序");
                    Thread.Sleep(2000);
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
            Instructions.Add(new(14, "斷開3V+", Order.SendPLCSignal, [Global.PLC, mem_3Vpos, (short)0])
            {
                OnStart = (Ins) => {
                    SysLog.Add(LogLevel.Info, $"斷開3V+ {mem_3Vpos}->0");
                }
            });
            Instructions.Add(new(14, "斷開3V-", Order.SendPLCSignal, [Global.PLC, mem_3Vneg, (short)0])
            {
                OnStart = (Ins) =>
                {
                    NextStep();//->"3V導通 LED閃爍檢測"
                    SysLog.Add(LogLevel.Info, $"斷開3V- {mem_3Vneg}->0");
                },
                OnEnd = (Ins) => Thread.Sleep(1000)
            });

            Instructions.Add(new(51, "升降汽缸下降程序", Order.WaitPLCSiganl, [Global.PLC, sensor_cyUD_UP, (short)1, 2000])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"確認汽缸在上定位 {sensor_cyUD_UP} == 1"),
                OnEnd = (Ins) => {
                    if (Ins.ExcResult == ExcResult.Success)
                    {
                        SysLog.Add(LogLevel.Info, "確認升降汽缸在上定位");
                        Thread.Sleep(500);
                        SysLog.Add(LogLevel.Info, $"升降汽缸下降 {cylinder_cyUD_DN} -> 1");
                        Global.PLC.WriteOneData(cylinder_cyUD_DN, 1);
                        Thread.Sleep(1000);
                    }
                    else
                    {
                        SysLog.Add(LogLevel.Error, $"升降汽缸不在上定位 {sensor_cyUD_UP}== 0");
                        Ins.ExcResult = ExcResult.Error;
                        return;
                    }
                    SysLog.Add(LogLevel.Info, $"等待升降汽缸下檢知 {sensor_cyUD_DN} == 1");
                    DateTime stT = DateTime.Now; bool err = false;
                    while (Global.PLC.ReadOneData(sensor_cyUD_DN).ReturnValue == 0)
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
                        SysLog.Add(LogLevel.Info, $"確認汽缸已在下定位 {sensor_cyUD_DN} == 1");
                        Global.PLC.WriteOneData(cylinder_cyUD_DN, 0);
                        SysLog.Add(LogLevel.Info, $"升降汽缸下降復歸 {cylinder_cyUD_DN} -> 0");
                    }

                }
            });
            Task.Run((Action)(() =>
            {
                this.SignalNext = false;
                if (IsModeStep)
                    SysLog.Add(LogLevel.Warning, "步進模式");
                foreach (Instruction ins in Instructions)
                {
                    "流程".TryCatch((Action)(() =>
                    {
                        if (IsModeStep)
                        {
                            SysLog.Add(LogLevel.Info, $"{ins.Title}:步進等待...");
                            while (!this.SignalNext)
                                Thread.Sleep(100);
                        }
                        int id = ins.ID ?? -1;
                        string title = ins.Title;
                        ins?.Execute();
                        this.SignalNext = false;
                        //if (ins != null && ins.ExcResult != ExcResult.Success)
                        //    return;
                    }));
                }
                this.SignalNext = false;
                IsBusy = false;
            }));
        }
        public void ProcedureBurn_ZBRT()
        {
            if (IsBusyBurn)
            {
                SysLog.Add(LogLevel.Warning, "燒錄流程正在執行");
                return;
            }
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
                    NextStep();
                }
            });
            Task.Run((Action)(() =>
            {
                "燒錄流程".TryCatch((Action)(() =>
                {
                    IsBusyBurn = true;
                    this.SignalNext = false;
                    if (IsModeStep)
                        SysLog.Add(LogLevel.Warning, "步進模式");
                    foreach (Instruction ins in InstructionsBurn)
                    {
                        if (IsBurnReseting)
                        {
                            this.SignalNext = false;
                            IsBusyBurn = false;
                            //ResetSteps();
                            return;
                        }
                        if (IsModeStep)
                        {
                            SysLog.Add(LogLevel.Info, $"{ins.Title}:步進等待...");
                            while (!this.SignalNext)
                                Thread.Sleep(100);
                        }
                        int id = ins.ID ?? -1;
                        string title = ins.Title;
                        ins?.Execute();
                        this.SignalNext = false;
                        if (ins != null && ins.ExcResult != ExcResult.Success)
                            return;
                    }
                }), (Action?)(() =>
                {
                    //ResetSteps();
                    this.SignalNext = false;
                    IsBusyBurn = false;
                }));
            }));
        }
    }
}
