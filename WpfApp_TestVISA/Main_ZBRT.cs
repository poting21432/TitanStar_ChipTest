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
            string memReady = "ZBRT_Signal_Ready".GetPLCMem();
            Instructions.Add(new(1, "工件放置確認", Order.WaitPLCSiganl, [Global.PLC, memReady, (short)1, 0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"等待工件放置完畢 {memReady} == 1"),
                OnEnd = (Ins) =>
                {
                }
            });
            Instructions.Add(new(2, "汽缸上升程序", Order.WaitPLCSiganl, [Global.PLC, "M4051", (short)1, 2000])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, "確認汽缸在下定位 M4051 == 1"),
                OnEnd = (Ins) => {
                    if (Ins.ExcResult == ExcResult.Success)
                    {
                        SysLog.Add(LogLevel.Info, "確認升降汽缸在下定位");
                        Thread.Sleep(500);
                        SysLog.Add(LogLevel.Info, "升降汽缸上升 M3059 -> 1");
                        Global.PLC.WriteOneData("M3059", 1);
                        Thread.Sleep(1000);
                    }
                    else
                    {
                        SysLog.Add(LogLevel.Error, "升降汽缸不在下定位 M4051 == 0");
                        Ins.ExcResult = ExcResult.Error;
                        return;
                    }
                    SysLog.Add(LogLevel.Info, "等待升降汽缸上檢知 M4050 == 1");
                    DateTime stT = DateTime.Now; bool err = false;
                    while (Global.PLC.ReadOneData("M4050").ReturnValue == 0)
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
                        SysLog.Add(LogLevel.Info, "確認汽缸已在上定位 M4050 == 1");
                        Global.PLC.WriteOneData("M3059", 0);
                        SysLog.Add(LogLevel.Info, "升降汽缸上升復歸 M3059 -> 0");
                    }

                }
            });
            Instructions.Add(new(5, "燒錄Pin導通", Order.SendPLCSignal, [Global.PLC, "M3054", (short)1])
            {
                OnStart = (Ins) =>
                {
                    SysLog.Add(LogLevel.Info, "燒錄Pin導通 M3054->1");
                    DispMain?.Invoke(() =>
                    {
                        ProductRecord newRecord = new();
                        CurrentProduct = newRecord;
                        ProductRecords.Add(newRecord);
                    });
                },
                OnEnd = (Ins) => NextStep() //->"燒錄處理"
            });
            Instructions.Add(new(6, "燒錄", Order.Custom, "PathBAT_ZBRT")
            {
                OnStart = (Ins) =>
                {
                    SysLog.Add(LogLevel.Warning, "略過燒錄程序");
                    Thread.Sleep(5000);
                    SysLog.Add(LogLevel.Info, "燒錄完成");
                },
                OnEnd = (Ins) => DispMain?.Invoke(() => CurrentProduct!.BurnCheck = "~")
            });
            Instructions.Add(new(7, "燒錄Pin斷開", Order.SendPLCSignal, [Global.PLC, "M3054", (short)0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, "燒錄Pin斷開 M3054 -> 0"),
                OnEnd = (Ins) => {
                    Thread.Sleep(1000);
                    NextStep();
                } //->"等待電測程序"
            });
            
            Instructions.Add(new(13, "測試開關汽缸下降", Order.SendPLCSignal, [Global.PLC, "M3050", (short)1])
            {
                OnStart = (Ins) => {
                    NextStep();//->"等待電表汽缸上升"
                    SysLog.Add(LogLevel.Info, "測試開關汽缸上升 M3050 -> 1");
                },
                OnEnd = (Ins) => Thread.Sleep(2000)
            });
            Instructions.Add(new(14, "導通3V+", Order.SendPLCSignal, [Global.PLC, "M3008", (short)1])
            {
                OnStart = (Ins) => {
                    SysLog.Add(LogLevel.Info, "導通3V M3008->1");
                }
            });
            Instructions.Add(new(14, "導通3V-", Order.SendPLCSignal, [Global.PLC, "M3009", (short)1])
            {
                OnStart = (Ins) => {
                    NextStep();//->"3V導通 LED閃爍檢測"
                    SysLog.Add(LogLevel.Info, "導通3V M3009->1");
                }
            });
            AddSigCount(15, "M4052", 4, () => DispMain?.Invoke(() => CurrentProduct!.OnCheck = "V"));
            Instructions.Add(new(16, "測試開關汽缸上升", Order.SendPLCSignal, [Global.PLC, "M3050", (short)0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, "測試開關汽缸上升 M3050 -> 0"),
                OnEnd = (Ins) => Thread.Sleep(2000)
            });
            
            Instructions.Add(new(29, "蓋開升降汽缸下降", Order.SendPLCSignal, [Global.PLC, "M3053", (short)1])
            {
                OnStart = (Ins) => {
                    NextStep(); //-> "蓋開 - LED檢測"
                    SysLog.Add(LogLevel.Info, "蓋開升降汽缸下降 M3053 -> 1");
                },
            });
            AddSigCount(30, "M4052", 1);
            Instructions.Add(new(31, "蓋開升降汽缸上升", Order.SendPLCSignal, [Global.PLC, "M3053", (short)0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, "蓋開升降汽缸上升 M3053 -> 0"),
            });
            AddSigCount(32, "M4052", 1, () => DispMain?.Invoke(() => CurrentProduct!.CoverCheck = "V"));
            
            Instructions.Add(new(33, "導通5V+", Order.SendPLCSignal, [Global.PLC, "M3051", (short)1])
            {
                OnStart = (Ins) =>
                {
                    Thread.Sleep(2000);
                    SysLog.Add(LogLevel.Info, "導通5V+ M3051->1");
                },
                OnEnd = (Ins) => Thread.Sleep(500)
            });
            Instructions.Add(new(33, "導通5V-", Order.SendPLCSignal, [Global.PLC, "M3052", (short)1])
            {
                OnStart = (Ins) =>
                {
                    NextStep(); //->"5V,mA 電表測試"
                    SysLog.Add(LogLevel.Info, "導通5V M3052->1");
                }
            });
            AddSigCount(34, "M4052", 1, () => DispMain?.Invoke(() => CurrentProduct!.Switch1Check = "V"));

            Instructions.Add(new(35, "斷開5V+", Order.SendPLCSignal, [Global.PLC, "M3051", (short)0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, "斷開5V+ M3051->0"),
            });
            Instructions.Add(new(35, "斷開5V-", Order.SendPLCSignal, [Global.PLC, "M3052", (short)0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, "斷開5V+ M3052->0"),
            });
            AddSigCount(34,"M4052", 1, () => DispMain?.Invoke(() => CurrentProduct!.ReedCheck = "V"));
            
            Instructions.Add(new(44, "2.4V導通", Order.SendPLCSignal, [Global.PLC, "M3058", (short)1])
            {
                OnStart = (Ins) =>
                {
                    NextStep(); //->"2.4V LED閃爍檢測"
                    SysLog.Add(LogLevel.Info, "2.4V導通 M3058 -> 1");
                }
            });

            Instructions.Add(new(44, "長亮檢查", Order.Custom)
            {
                OnStart = (Ins) =>
                {
                    while (Global.PLC.ReadOneData("M4052").ReturnValue == 0)
                        Thread.Sleep(100);
                    Thread.Sleep(1000);
                    if (Global.PLC.ReadOneData("M4052").ReturnValue == 1)
                        return;
                }
            });
            
            Instructions.Add(new(46, "2.4V斷開", Order.SendPLCSignal, [Global.PLC, "M3058", (short)0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, "2.4V斷開 M3058 -> 0"),
            });

            Instructions.Add(new(47, "測試開關汽缸下降", Order.SendPLCSignal, [Global.PLC, "M3050", (short)1])
            {
                OnStart = (Ins) =>
                {
                    NextStep(); //->"2.4V LED閃爍檢測"
                    SysLog.Add(LogLevel.Info, "測試開關汽缸下降 M3050 -> 1");
                }
            });
            AddSigCount(48, "M4052", 1, () => DispMain?.Invoke(() => CurrentProduct!.OnOffCheck = "V"));
            Instructions.Add(new(49, "測試開關汽缸上升", Order.SendPLCSignal, [Global.PLC, "M3050", (short)0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, "測試開關汽缸上升 M3050 -> 0"),
            });

            Instructions.Add(new(50, "頻譜儀天線強度測試", Order.Custom)
            {
                OnStart = (Ins) =>
                {
                    NextStep();  //"頻譜儀天線強度測試"
                    DispMain?.Invoke(() => CurrentProduct!.TestAntenna = "~");
                    SysLog.Add(LogLevel.Warning, "略過頻譜儀天線強度測試程序");
                    Thread.Sleep(2000);
                },
            });

            //M3013 -> 1 升降汽缸下降
            //M4001 -> 1 確認下定位
            //M3013 -> 0 復歸
            Instructions.Add(new(51, "升降汽缸下降程序", Order.WaitPLCSiganl, [Global.PLC, "M4050", (short)1, 2000])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, "確認汽缸在上定位 M4050 == 1"),
                OnEnd = (Ins) => {
                    if (Ins.ExcResult == ExcResult.Success)
                    {
                        SysLog.Add(LogLevel.Info, "確認升降汽缸在上定位");
                        Thread.Sleep(500);
                        SysLog.Add(LogLevel.Info, "升降汽缸下降 M3060 -> 1");
                        Global.PLC.WriteOneData("M3060", 1);
                        Thread.Sleep(1000);
                    }
                    else
                    {
                        SysLog.Add(LogLevel.Error, "升降汽缸不在下定位 M4050 == 0");
                        Ins.ExcResult = ExcResult.Error;
                        return;
                    }
                    SysLog.Add(LogLevel.Info, "等待升降汽缸下檢知 M4051 == 1");
                    DateTime stT = DateTime.Now; bool err = false;
                    while (Global.PLC.ReadOneData("M4051").ReturnValue == 0)
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
                        SysLog.Add(LogLevel.Info, "確認汽缸已在下定位 M4051 == 1");
                        Global.PLC.WriteOneData("M3060", 0);
                        SysLog.Add(LogLevel.Info, "升降汽缸下降復歸 M3013 -> 0");
                    }

                }
            });
            Instructions.Add(new(52, "測試完畢", Order.SendPLCSignal, [Global.PLC, "M4450", (short)1])
            {
                OnEnd = (Ins) =>
                {
                    NextStep();
                    SysLog.Add(LogLevel.Success, "產品作業完成 M4450 -> 1");
                    Thread.Sleep(5000);
                },
            });
            Task.Run(() =>
            {
                "流程".TryCatch(() =>
                {
                    SignalNext = false;
                    if (IsModeStep)
                        SysLog.Add(LogLevel.Warning, "步進模式");
                    foreach (Instruction ins in Instructions)
                    {
                        if (IsModeStep)
                        {
                            SysLog.Add(LogLevel.Info, $"{ins.Title}:步進等待...");
                            while (!SignalNext)
                                Thread.Sleep(100);
                        }
                        int id = ins.ID ?? -1;
                        string title = ins.Title;
                        ins?.Execute();
                        SignalNext = false;
                        if (ins != null && ins.ExcResult != ExcResult.Success)
                            return;
                    }
                }, () =>
                {
                    SignalNext = false;
                    IsBusy = false;
                });
            });
        }
    }
}
