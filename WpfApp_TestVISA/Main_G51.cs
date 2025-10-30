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
        public void ProcedureMain()
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
            Instructions.Add(new(1, "工件放置確認", Order.WaitPLCSiganl, [Global.PLC, "D3000", (short)1, 0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, "等待工件放置完畢 D3000 == 1"),
                OnEnd = (Ins) =>
                {
                    if (Ins.ExcResult == ExcResult.Success)
                        SysLog.Add(LogLevel.Info, "確認工件放置完畢");
                }
            });
            Instructions.Add(new(2, "汽缸上升程序", Order.WaitPLCSiganl, [Global.PLC, "M4001", (short)1, 2000])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, "確認汽缸在下定位 M4001 == 1"),
                OnEnd = (Ins) => {
                    if (Ins.ExcResult == ExcResult.Success)
                    {
                        SysLog.Add(LogLevel.Info, "確認升降汽缸在下定位");
                        Thread.Sleep(500);
                        SysLog.Add(LogLevel.Info, "升降汽缸上升 M3012 -> 1");
                        Global.PLC.WriteOneData("M3012", 1);
                        Thread.Sleep(1000);
                    }
                    else
                    {
                        SysLog.Add(LogLevel.Error, "升降汽缸不在下定位 M4001 == 0");
                        Ins.ExcResult = ExcResult.Error;
                        return;
                    }
                    SysLog.Add(LogLevel.Info, "等待升降汽缸上檢知 M4000 == 1");
                    DateTime stT = DateTime.Now; bool err = false;
                    while (Global.PLC.ReadOneData("M4000").ReturnValue == 0)
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
                        SysLog.Add(LogLevel.Info, "確認汽缸已在上定位 M4000 == 1");
                        Global.PLC.WriteOneData("M3012", 0);
                        SysLog.Add(LogLevel.Info, "升降汽缸上升復歸 M3012 -> 0");
                    }

                }
            });
            Instructions.Add(new(5, "燒錄Pin導通", Order.SendPLCSignal, [Global.PLC, "M3007", (short)1])
            {
                OnStart = (Ins) =>
                {
                    SysLog.Add(LogLevel.Info, "燒錄Pin導通 M3007->1");
                    DispMain?.Invoke(() =>
                    {
                        ProductRecord newRecord = new();
                        CurrentProduct = newRecord;
                        ProductRecords.Add(newRecord);
                    });
                },
                OnEnd = (Ins) => NextStep() //->"燒錄處理"
            });
            Instructions.Add(new(6, "燒錄", Order.Custom)
            {
                OnStart = (Ins) =>
                {
                    SysLog.Add(LogLevel.Warning, "略過燒錄程序");
                    Thread.Sleep(5000);
                    SysLog.Add(LogLevel.Info, "燒錄完成");
                },
                OnEnd = (Ins) => DispMain?.Invoke(() => CurrentProduct!.BurnCheck = "~")
            });
            Instructions.Add(new(7, "燒錄Pin斷開", Order.SendPLCSignal, [Global.PLC, "M3007", (short)0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, "燒錄Pin斷開 M3007 -> 0"),
                OnEnd = (Ins) => {
                    Thread.Sleep(1000);
                    NextStep();
                } //->"等待電測程序"
            });
            Instructions.Add(new(8, "切電表至低量程", Order.SendModbus, [(ushort)0x001F, (ushort)1])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, "設定電表至低量程:(0x001F)->1")
            });
            Instructions.Add(new(8, "檢查電表為低量程", Order.WaitModbus, [(ushort)0x37, (ushort)1, 5000])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, "檢查電表為低量程:(0x37) == 1"),
                OnEnd = (Ins) =>
                {
                    if (Ins.ExcResult == ExcResult.Success)
                        SysLog.Add(LogLevel.Info, "已確認電表: 低量程");
                    else
                        SysLog.Add(LogLevel.Error, "電表量程檢查失敗");
                }
            });
            Instructions.Add(new(9, "導通3V+", Order.SendPLCSignal, [Global.PLC, "M3008", (short)1])
            {
                OnStart = (Ins) =>
                {
                    Thread.Sleep(2000);
                    SysLog.Add(LogLevel.Info, "導通3V+ M3008->1");
                },
                OnEnd = (Ins) => Thread.Sleep(500)
            });
            Instructions.Add(new(9, "導通3V-", Order.SendPLCSignal, [Global.PLC, "M3009", (short)1])
            {
                OnStart = (Ins) =>
                {
                    NextStep(); //-> "3V,uA 電表測試"
                    SysLog.Add(LogLevel.Info, "導通3V- M3009->1");
                },
                OnEnd = (Ins) => Thread.Sleep(1000)
            });
            Instructions.Add(new(10, "讀電表值(低量程)", Order.ReadModbusFloat, [(ushort)0x0030])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, "讀取低量程電表數值:(0x0030)"),
                OnEnd = (Ins) =>
                {
                    float? uA = Ins.Result as float?;
                    if (!uA.HasValue)
                        Ins.ExcResult = ExcResult.Error;
                    if (uA >= 8.0)
                    {
                        SysLog.Add(LogLevel.Warning, $"電表數值過高(低量程 < 8.0):{Ins.Result}");
                        Ins.ExcResult = ExcResult.Error;
                    }

                    if (Ins.ExcResult == ExcResult.Success)
                    {
                        SysLog.Add(LogLevel.Info, $"讀取電表數值(低量程):{Ins.Result}");
                        DispMain?.Invoke(() =>
                        {
                            if (CurrentProduct != null)
                                CurrentProduct.Test3VuA = uA;
                        });
                    }

                    else
                        SysLog.Add(LogLevel.Error, "電表數值異常");
                }
            });
            Instructions.Add(new(11, "斷開3V+", Order.SendPLCSignal, [Global.PLC, "M3008", (short)0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, "斷開3V M3008->0"),
                OnEnd = (Ins) => Thread.Sleep(500)
            });
            Instructions.Add(new(11, "斷開3V-", Order.SendPLCSignal, [Global.PLC, "M3009", (short)0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, "斷開3V M3009->0"),
                OnEnd = (Ins) => Thread.Sleep(1000)
            });
            Instructions.Add(new(12, "切電表至高量程", Order.SendModbus, [(ushort)0x001F, 2])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, "設定電表至高量程:(0x001F)->2")
            });
            Instructions.Add(new(12, "檢查電表為高量程", Order.WaitModbus, [(ushort)0x37, (ushort)2, 5000])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, "檢查電表為高量程:(0x37) == 2"),
                OnEnd = (Ins) =>
                {
                    if (Ins.ExcResult == ExcResult.Success)
                        SysLog.Add(LogLevel.Info, "已確認電表: 高量程");
                    else
                        SysLog.Add(LogLevel.Error, "電表量程檢查失敗");
                }
            });
            Instructions.Add(new(13, "測試開關汽缸上升", Order.SendPLCSignal, [Global.PLC, "M3006", (short)1])
            {
                OnStart = (Ins) => {
                    NextStep();//->"等待電表汽缸上升"
                    SysLog.Add(LogLevel.Info, "測試開關汽缸上升 M3006 -> 1");
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
            AddSigCount(15, "M4003", 4, () => DispMain?.Invoke(() => CurrentProduct!.OnCheck = "V"));
            Instructions.Add(new(16, "測試開關汽缸下降", Order.SendPLCSignal, [Global.PLC, "M3006", (short)0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, "測試開關汽缸下降 M3006 -> 0"),
                OnEnd = (Ins) => Thread.Sleep(2000)
            });
            Instructions.Add(new(17, "IO點位測試開", Order.SendPLCSignal, [Global.PLC, "M3000", (short)1])
            {
                OnStart = (Ins) => {
                    NextStep();//-> "DIO探針LED檢測
                    SysLog.Add(LogLevel.Info, "IO點位測試導通 M3000 -> 1");
                }
            });
            AddSigCount(18, "M4003", 1);
            Instructions.Add(new(19, "IO點位測試關", Order.SendPLCSignal, [Global.PLC, "M3000", (short)0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, "IO點位測試關閉 M3000 -> 0"),
            });
            AddSigCount(20, "M4003", 1, () => DispMain?.Invoke(() => CurrentProduct!.DIOCheck = "V"));
            Instructions.Add(new(21, "指撥-1導通", Order.SendPLCSignal, [Global.PLC, "M3001", (short)1])
            {
                OnStart = (Ins) =>
                {
                    NextStep(); //-> "指撥1 - LED檢測"
                    SysLog.Add(LogLevel.Info, "指撥開關-1導通 M3001 -> 1");
                }
            });
            AddSigCount(22, "M4003", 1);
            Instructions.Add(new(23, "指撥-1斷開", Order.SendPLCSignal, [Global.PLC, "M3001", (short)0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, "指撥開關-1斷開 M3001 -> 0"),
            });
            AddSigCount(24, "M4003", 1, () => DispMain?.Invoke(() => CurrentProduct!.Switch1Check = "V"));
            Instructions.Add(new(25, "指撥-2導通", Order.SendPLCSignal, [Global.PLC, "M3002", (short)1])
            {
                OnStart = (Ins) =>
                {
                    NextStep(); //-> "指撥2 - LED檢測"
                    SysLog.Add(LogLevel.Info, "指撥開關-2導通 M3002 -> 1");
                }
            });
            AddSigCount(26, "M4003", 1);
            Instructions.Add(new(27, "指撥-2斷開", Order.SendPLCSignal, [Global.PLC, "M3002", (short)0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, "指撥開關-1導通 M3002 -> 0"),
            });
            AddSigCount(28, "M4003", 1, () => DispMain?.Invoke(() => CurrentProduct!.Switch2Check = "V"));
            Instructions.Add(new(29, "蓋開升降汽缸下降", Order.SendPLCSignal, [Global.PLC, "M3005", (short)1])
            {
                OnStart = (Ins) => {
                    NextStep(); //-> "蓋開 - LED檢測"
                    SysLog.Add(LogLevel.Info, "蓋開升降汽缸下降 M3005 -> 1");
                },
            });
            AddSigCount(30, "M4003", 1);
            Instructions.Add(new(31, "蓋開升降汽缸上升", Order.SendPLCSignal, [Global.PLC, "M3005", (short)0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, "蓋開升降汽缸上升 M3005 -> 0"),
            });
            AddSigCount(32, "M4003", 1, () => DispMain?.Invoke(() => CurrentProduct!.CoverCheck = "V"));
            Instructions.Add(new(33, "檢查電表為高量程", Order.WaitModbus, [(ushort)0x37, (ushort)2, 5000])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, "檢查電表為高量程:(0x37) == 2"),
                OnEnd = (Ins) =>
                {
                    if (Ins.ExcResult == ExcResult.Success)
                        SysLog.Add(LogLevel.Info, "已確認電表: 高量程");
                    else
                        SysLog.Add(LogLevel.Error, "電表量程檢查失敗");
                }
            });
            Instructions.Add(new(33, "導通5V+", Order.SendPLCSignal, [Global.PLC, "M3003", (short)1])
            {
                OnStart = (Ins) =>
                {
                    Thread.Sleep(2000);
                    SysLog.Add(LogLevel.Info, "導通5V+ M3003->1");
                },
                OnEnd = (Ins) => Thread.Sleep(500)
            });
            Instructions.Add(new(33, "導通5V-", Order.SendPLCSignal, [Global.PLC, "M3004", (short)1])
            {
                OnStart = (Ins) =>
                {
                    NextStep(); //->"5V,mA 電表測試"
                    SysLog.Add(LogLevel.Info, "導通5V M3004->1");
                },
                OnEnd = (Ins) => Thread.Sleep(1000)
            });
            Instructions.Add(new(34, "讀電表值(高量程)", Order.ReadModbusFloat, [(ushort)0x0032])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, "讀取高量程電表數值:(0x0032)"),
                OnEnd = (Ins) =>
                {
                    float? v = Ins.Result as float?;
                    if (!v.HasValue)
                        Ins.ExcResult = ExcResult.Error;
                    if (v > 2.0)
                    {
                        SysLog.Add(LogLevel.Warning, $"電表數值過高(高量程 < 2.0):{Ins.Result}");
                        Ins.ExcResult = ExcResult.Error;
                    }
                    if (Ins.ExcResult == ExcResult.Success)
                    {
                        SysLog.Add(LogLevel.Info, $"讀取電表數值(高量程):{Ins.Result}");
                        DispMain?.Invoke(() => CurrentProduct!.Test5VmA = v);
                    }
                    else
                        SysLog.Add(LogLevel.Error, "電表數值異常");
                }
            });
            Instructions.Add(new(35, "斷開5V+", Order.SendPLCSignal, [Global.PLC, "M3003", (short)0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, "斷開5V+ M3003->0"),
                OnEnd = (Ins) => Thread.Sleep(500)
            });
            Instructions.Add(new(35, "斷開5V-", Order.SendPLCSignal, [Global.PLC, "M3004", (short)0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, "斷開5V+ M3004->0"),
                OnEnd = (Ins) => Thread.Sleep(1000)
            });
            Instruction ins_check1 = new(38, $"閃爍檢測1次", Order.PLCSignalCount, [Global.PLC, "M4003", (short)1, 5000])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"閃爍檢測 1次: M4003 ^v 1"),
                OnEnd = (Ins) =>
                {
                    if (Ins.ExcResult != ExcResult.Success)
                        SysLog.Add(LogLevel.Error, "閃爍檢測計數錯誤");
                    else
                    {
                        SysLog.Add(LogLevel.Info, $"確認閃爍1次");
                        Thread.Sleep(2000);
                    }
                }
            };
            Instructions.Add(new(36, "磁簧感應汽缸下降", Order.SendPLCSignal, [Global.PLC, "M3014", (short)1])
            {
                OnStart = (Ins) => {
                    Task.Run(() => {
                        ins_check1.Execute();
                    });
                    NextStep();//->"磁簧汽缸 - LED檢測"
                    SysLog.Add(LogLevel.Info, "磁簧感應汽缸下降 M3014 -> 1");
                },
            });

            Instructions.Add(new(37, "確認磁簧感應汽缸下降", Order.WaitPLCSiganl, [Global.PLC, "M4002", (short)1, 5000])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, "等待磁簧感應汽缸下降 M4002 -> 1"),
                OnEnd = (Ins) =>
                {
                    while (ins_check1.ExcResult != ExcResult.Success)
                    {
                        if (ins_check1.ExcResult == ExcResult.TimeOut)
                        {
                            Ins.ExcResult = ExcResult.Error;
                            return;
                        }
                        Thread.Sleep(200);
                    }
                    if (Ins.ExcResult != ExcResult.Success)
                        SysLog.Add(LogLevel.Error, "磁簧感應汽缸下降異常");
                    else
                        SysLog.Add(LogLevel.Error, "確認磁簧感應汽缸下降");
                }
            });
            Instruction ins_check2 = new(41, $"閃爍檢測1次", Order.PLCSignalCount, [Global.PLC, "M4003", (short)1, 5000])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"閃爍檢測 1次: M4003 ^v 1"),
                OnEnd = (Ins) =>
                {
                    if (Ins.ExcResult != ExcResult.Success)
                        SysLog.Add(LogLevel.Error, "閃爍檢測計數錯誤");
                    else
                    {
                        SysLog.Add(LogLevel.Info, $"確認閃爍1次");
                        Thread.Sleep(2000);
                    }
                }
            };
            Instructions.Add(new(39, "磁簧感應汽缸上升", Order.SendPLCSignal, [Global.PLC, "M3014", (short)0])
            {
                OnStart = (Ins) => {
                    Task.Run(() => {
                        ins_check2.Execute();
                    });
                    SysLog.Add(LogLevel.Info, "磁簧感應汽缸上升 M3014 -> 0");
                },
            });
            Instructions.Add(new(40, "確認磁簧感應汽缸上升", Order.WaitPLCSiganl, [Global.PLC, "M4002", (short)0, 5000])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, "等待磁簧感應汽缸上升 M4002 -> 0"),
                OnEnd = (Ins) =>
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
                    if (Ins.ExcResult != ExcResult.Success)
                        SysLog.Add(LogLevel.Error, "磁簧感應汽缸上升異常");
                    else
                        SysLog.Add(LogLevel.Error, "確認磁簧感應汽缸上升");
                }
            });
            AddSigCount(41, "M4003", 1, () => DispMain?.Invoke(() => CurrentProduct!.ReedCheck = "V"));
            Instructions.Add(new(43, "3V+斷開(電流表)", Order.SendPLCSignal, [Global.PLC, "M3008", (short)0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, "3V+斷開(電流表) M3008 -> 0"),
                OnEnd = (Ins) => Thread.Sleep(500)
            });
            Instructions.Add(new(42, "3V+導通(探針)", Order.SendPLCSignal, [Global.PLC, "M3010", (short)1])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, "3V+導通(探針) M3010-> 1"),
                OnEnd = (Ins) => Thread.Sleep(1000)
            });

            Instructions.Add(new(44, "2.4V導通", Order.SendPLCSignal, [Global.PLC, "M3011", (short)1])
            {
                OnStart = (Ins) =>
                {
                    NextStep(); //->"2.4V LED閃爍檢測"
                    SysLog.Add(LogLevel.Info, "2.4V導通 M3011 -> 1");
                }
            });
            AddSigCount(45, "M4003", 5, () => DispMain?.Invoke(() => CurrentProduct!.LowVCheck = "V"));
            Instructions.Add(new(46, "2.4V斷開", Order.SendPLCSignal, [Global.PLC, "M3011", (short)0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, "2.4V斷開 M3011 -> 0"),
            });
            Instructions.Add(new(47, "測試開關汽缸上升", Order.SendPLCSignal, [Global.PLC, "M3006", (short)1])
            {
                OnStart = (Ins) =>
                {
                    NextStep(); //->"2.4V LED閃爍檢測"
                    SysLog.Add(LogLevel.Info, "測試開關汽缸上升 M3006 -> 1");
                }
            });
            AddSigCount(48, "M4003", 1, () => DispMain?.Invoke(() => CurrentProduct!.OnOffCheck = "V"));
            Instructions.Add(new(49, "測試開關汽缸下降", Order.SendPLCSignal, [Global.PLC, "M3006", (short)0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, "測試開關汽缸下降 M3006 -> 0"),
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
            Instructions.Add(new(51, "升降汽缸下降程序", Order.WaitPLCSiganl, [Global.PLC, "M4000", (short)1, 2000])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, "確認汽缸在上定位 M4000 == 1"),
                OnEnd = (Ins) => {
                    if (Ins.ExcResult == ExcResult.Success)
                    {
                        SysLog.Add(LogLevel.Info, "確認升降汽缸在上定位");
                        Thread.Sleep(500);
                        SysLog.Add(LogLevel.Info, "升降汽缸下降 M3013 -> 1");
                        Global.PLC.WriteOneData("M3013", 1);
                        Thread.Sleep(1000);
                    }
                    else
                    {
                        SysLog.Add(LogLevel.Error, "升降汽缸不在下定位 M4000 == 0");
                        Ins.ExcResult = ExcResult.Error;
                        return;
                    }
                    SysLog.Add(LogLevel.Info, "等待升降汽缸下檢知 M4001 == 1");
                    DateTime stT = DateTime.Now; bool err = false;
                    while (Global.PLC.ReadOneData("M4001").ReturnValue == 0)
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
                        SysLog.Add(LogLevel.Info, "確認汽缸已在下定位 M4001 == 1");
                        Global.PLC.WriteOneData("M3013", 0);
                        SysLog.Add(LogLevel.Info, "升降汽缸下降復歸 M3013 -> 0");
                    }

                }
            });
            Instructions.Add(new(52, "測試完畢", Order.SendPLCSignal, [Global.PLC, "M4400", (short)1])
            {
                OnEnd = (Ins) =>
                {
                    NextStep();
                    SysLog.Add(LogLevel.Success, "產品作業完成 M4400 -> 1");
                    Thread.Sleep(2000);
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
