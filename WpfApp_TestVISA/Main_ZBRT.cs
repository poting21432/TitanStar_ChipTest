using PropertyChanged;
using Support;
using Support.Data;
using Support.Logger;
using Support.Wpf.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;

namespace WpfApp_TitanStar_TestPlatform
{
    public partial class Model_Main
    {
        static Lazy<string> ZBRT_v5_pos = new(()=> "ZBRT_Supply_5V+".GetPLCMem());//M3051
        static Lazy<string> ZBRT_v5_neg = new(()=> "ZBRT_Supply_5V-".GetPLCMem());//M3052
        static Lazy<string> ZBRT_v3_pos = new(()=> "ZBRT_Supply_3V+".GetPLCMem());//M3055
        static Lazy<string> ZBRT_v3_neg = new(()=> "ZBRT_Supply_3V-".GetPLCMem());//M3056
        static Lazy<string> ZBRT_v_low = new (()=>"ZBRT_Supply_2.4V".GetPLCMem());//M3058
        public ProcedureState ZBRTBurnState { get; set; } = new("ZBRT燒錄程序");
        public ProcedureState ZBRTTestState { get; set; } = new("ZBRT測試程序");

        internal static readonly string[] StrSteps_ZBRT = [
            "等待燒錄到位" ,"燒錄中", "燒錄完成復歸",
            "等待測試到位","3V導通-LED檢測", "蓋開-LED檢測",
            "低電壓-LED檢測", "測試開關-LED檢測", "頻譜儀天線強度測試",
            "產品完成送出"
        ];
        #region ZBRT Related Commands
        public ICommand? Command_ZBRT_3VON { get; set; }
        public ICommand? Command_ZBRT_5VON { get; set; }
        public ICommand? Command_ZBRT_LowV { get; set; }
        public ICommand? Command_ZBRT_OFF { get; set; }
        private void InitializeCommands_ZBRT()
        {
            Command_ZBRT_3VON = new RelayCommand<object>((obj) =>
            {
                if (!Global.IsInitialized) return;
                Global.PLC.WriteRandomData([ZBRT_v5_pos.Value, ZBRT_v5_pos.Value, ZBRT_v_low.Value], [0, 0, 0]);
                Thread.Sleep(300);
                Global.PLC.WriteRandomData([ZBRT_v3_pos.Value, ZBRT_v3_neg.Value], [1, 1]);
                ZBRT_Supply = "3V";
                SysLog.Add(LogLevel.Warning, $"手動切換電壓(ZBRT):{ZBRT_Supply}");
            });
            Command_ZBRT_5VON = new RelayCommand<object>((obj) =>
            {
                if (!Global.IsInitialized) return;
                Global.PLC.WriteRandomData([ZBRT_v_low.Value, ZBRT_v3_pos.Value, ZBRT_v3_neg.Value], [0, 0, 0]);
                Thread.Sleep(300);
                Global.PLC.WriteRandomData([ZBRT_v5_pos.Value, ZBRT_v5_neg.Value], [1, 1]);
                ZBRT_Supply = "5V";
                SysLog.Add(LogLevel.Warning, $"手動切換電壓(ZBRT):{ZBRT_Supply}");
            });
            Command_ZBRT_LowV = new RelayCommand<object>((obj) =>
            {
                if (!Global.IsInitialized) return;
                Global.PLC.WriteRandomData([ZBRT_v5_pos.Value, ZBRT_v5_neg.Value, ZBRT_v_low.Value], [0, 0, 0]);
                Thread.Sleep(300);
                Global.PLC.WriteRandomData([ZBRT_v3_pos.Value, ZBRT_v3_neg.Value], [1, 1]);
                Thread.Sleep(100);
                Global.PLC.WriteRandomData([ZBRT_v_low.Value], [1]);
                ZBRT_Supply = "2.4V";
                SysLog.Add(LogLevel.Warning, $"手動切換電壓(ZBRT):{ZBRT_Supply}");
            });

            Command_ZBRT_OFF = new RelayCommand<object>((obj) =>
            {
                if (!Global.IsInitialized) return;
                Global.PLC.WriteRandomData([ZBRT_v3_pos.Value, ZBRT_v3_neg.Value, ZBRT_v5_pos.Value, ZBRT_v5_neg.Value, ZBRT_v_low.Value],
                                           [0, 0, 0, 0, 0]);
                ZBRT_Supply = "OFF";
                SysLog.Add(LogLevel.Warning, $"手動切換電壓(ZBRT):{ZBRT_Supply}");
            });
        }
        #endregion
        #region ZBRT Test Sequence
        public void ProcedureTest_ZBRT()
        {
            CurrentTestState = ZBRTTestState;
            ProcedureState PState = ZBRTTestState;
            if (PState.IsBusy)
            {
                SysLog.Add(LogLevel.Warning, $"{PState.Title}正在執行，先終止後重啟");
                return;
            }
            PState.SetStart();
            #region ZBRT Test Instructions
            Instructions.Clear();
            string memReady = "ZBRT_Signal_Ready".GetPLCMem();
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
            string mem_result = "ZBRT_Signal_Result".GetPLCMem();//D3031

            InitTaskedInstructions(photoresistor);
            Instructions.Add(new(1, "工件放置確認", Order.WaitPLCSiganl, [Global.PLC, memReady, (short)1, 0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"等待工件放置完畢 {memReady} == 1")
            });
            
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
                    NextStep(); //"3V導通-LED檢測"
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
                    CreateInstructionTask("頻譜儀初始化", ins_initRF);
                    CreateInstructionTask("3V測試導通", ins_pho_check4);
                    SysLog.Add(LogLevel.Info, $"導通3V {v3_pos}->1 {v3_neg}->1");
                    Global.PLC.WriteRandomData([v3_pos, v3_neg], [1, 1]);
                },
                OnEnd = (Ins) => {
                    Thread.Sleep(100);
                    Ins.ExcResult = WaitPhoCheck(Ins, ins_pho_check4,
                         () => CurrentProduct!.OnCheck = "V",
                         () => CurrentProduct!.OnCheck = "X");
                }
            });
            Instructions.Add(new(16, "登錄開關上升", Order.SendPLCSignal, [Global.PLC, mem_switch, (short)0])
            {
                OnStart = (Ins) => {
                    CreateInstructionTask("登錄開關上升", ins_pho_check1);
                    SysLog.Add(LogLevel.Info, $"登錄開關上升 {mem_switch} -> 0");
                },
                OnEnd =(Ins) => Ins.ExcResult = WaitPhoCheck(Ins, ins_pho_check1)
            });
            Instructions.Add(new(29, "蓋開汽缸下降", Order.SendPLCSignal, [Global.PLC, cyl_cover, (short)1])
            {
                OnStart = (Ins) => {
                    CreateInstructionTask("蓋開汽缸下降", ins_pho_check1);
                    NextStep();
                    SysLog.Add(LogLevel.Info, $"蓋開汽缸下降 {cyl_cover} -> 1");
                },
                OnEnd = (Ins) =>
                    Ins.ExcResult = WaitPhoCheck(Ins, ins_pho_check1,
                        null, () => CurrentProduct!.CoverCheck = "X")
            });
            Instructions.Add(new(31, "蓋汽缸上升", Order.SendPLCSignal, [Global.PLC, cyl_cover, (short)0])
            {
                OnStart = (Ins) =>
                {
                    CreateInstructionTask("蓋開汽缸上升", ins_pho_check1);
                    SysLog.Add(LogLevel.Info, $"蓋開汽缸上升 {cyl_cover} -> 0");
                },
                OnEnd = (Ins) =>
                    Ins.ExcResult = WaitPhoCheck(Ins, ins_pho_check1,
                        () => CurrentProduct!.CoverCheck = "V",
                        () => CurrentProduct!.CoverCheck = "X")
            });

            Instructions.Add(new(33, "斷開5V", Order.Custom)
            {
                OnStart = (Ins) =>
                {
                    CreateInstructionTask("斷開5V", ins_pho_check1);
                    SysLog.Add(LogLevel.Info, $"斷開5V {v5_pos}->0 {v5_neg}->0");
                    Global.PLC.WriteRandomData([v5_pos, v5_neg], [0, 0]);
                    Ins.ExcResult = ExcResult.Success;
                },
                OnEnd = (Ins) => Ins.ExcResult = WaitPhoCheck(Ins, ins_pho_check1)
            });

            Instructions.Add(new(44, "2.4V導通", Order.SendPLCSignal, [Global.PLC, v_low, (short)1])
            {
                OnStart = (Ins) =>
                {
                    NextStep();
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
                    Thread.Sleep(200);
                },
            });
            Instructions.Add(new(49, "測試開關上升", Order.SendPLCSignal, [Global.PLC, mem_switch, (short)0])
            {
                OnStart = (Ins) =>
                {
                    CreateInstructionTask("測試開關上升", ins_pho_check1);
                    SysLog.Add(LogLevel.Info, $"測試開關上升 {mem_switch} -> 0");
                },
                OnEnd = (Ins) => Ins.ExcResult = WaitPhoCheck(Ins, ins_pho_check1,
                                                             () => CurrentProduct!.OnOffCheck = "V",
                                                             () => CurrentProduct!.OnOffCheck = "X")
            });
            
            Instructions.Add(new(50, "頻譜儀天線強度測試", Order.Custom)
            {
                OnStart = async (Ins) =>
                {
                    NextStep();  //"頻譜儀天線強度測試"
                    double value = await ReadRFValue();
                    Ins.ExcResult = double.IsNaN(value) ? ExcResult.Error : ExcResult.Success;
                },
            });
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
            #endregion
            Task.Run(() =>
            {
                if (CurrentStepID != 4)
                {
                    if (CurrentProduct == null)
                        DispMain?.Invoke(() => {
                            CurrentProduct = new() { TimeStart = DateTime.Now };
                            ProductRecords.Add(CurrentProduct);
                        });
                    ResetSteps(4, setStart: true);
                }
                DispMain?.Invoke(() =>
                {
                    CurrentProduct!.DIOCheck = "-";
                    CurrentProduct.Switch1Check = "-";
                    CurrentProduct.Switch2Check = "-";
                });
                PState.SignalNext = false;
                if (PState.IsModeStep)
                    SysLog.Add(LogLevel.Warning, "步進模式");

                foreach (Instruction ins in Instructions)
                {
                    if (PState.IsCompleted)
                        return;
                    DispMain?.Invoke(() => PState.CurrentInstruction = ins);
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
                            PState.SetEnd(ExcResult.Abort, () => ErrorStep());
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
                            PState.SetEnd(ins.ExcResult, () => ErrorStep());
                            ErrorStep();
                            ZBRTTestNGRelease();
                            return;
                        }
                    });
                    if (!ex)
                    {
                        PState.SetEnd(ExcResult.Error, () => ErrorStep());
                        ErrorStep();
                        ZBRTTestNGRelease();
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
        private void ZBRTTestNGRelease()
        {
            string mem_result = "ZBRT_Signal_Result".GetPLCMem();//M4400
            SysLog.Add(LogLevel.Warning, $"{ZBRTTestState.Title}:{SelectedResetOption}復歸...");
            Global.PLC.WriteOneData(mem_result, PLCResetResult);
            if (CurrentProduct != null)
                DispMain?.Invoke(() => CurrentProduct.TimeEnd = DateTime.Now);
            if (PLCResetResult != 0)
                ProcedureZBRTResetTest();
            CurrentProduct = null;
        }
        #endregion
        #region ZBRT Burn Sequence
        public void ProcedureBurn_ZBRT()
        {
            CurrentBurnState = ZBRTBurnState;
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
            string memReady = "ZBRT_Burn_Ready".GetPLCMem();
            string mem_burn_result = "ZBRT_Burn_Result".GetPLCMem();//D3021
            #region ZBRT Burn Instructions
            InstructionsBurn.Clear();
            InstructionsBurn.Add(new(1, "工件放置確認", Order.WaitPLCSiganl, [Global.PLC, memReady, (short)1, 0])
            {
                OnStart = (Ins) => SysLog.Add(LogLevel.Info, $"等待燒錄工件放置完畢 {memReady} == 1"),
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
            InstructionsBurn.Add(new(2, "燒錄", Order.Burn, "PathBAT_ZBRT")
            {
                OnStart = (Ins) => NextStep(),
                OnEnd = (Ins) => 
                {
                    DispMain?.Invoke(() => CurrentProduct!.BurnCheck = (Ins.ExcResult == ExcResult.Success) ? "V" : "X");
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
                    NextStep();
                }
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
                    SysLog.Add(LogLevel.Warning, "步進模式");

                foreach (Instruction ins in InstructionsBurn)
                {
                    if (PState.IsCompleted)
                        return;
                    DispMain?.Invoke(() => PState.CurrentInstruction = ins);
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
                            PState.SetEnd(ExcResult.Abort, () => ErrorStep());
                            return;
                        }
                        int id = ins.ID ?? -1;
                        string title = ins.Title;
                        ins?.Execute();
                        PState.SignalNext = false;
                        if (!PState.IsBypassErr && ins != null &&
                            ins.ExcResult != ExcResult.Success)
                        {
                            PState.SetEnd(ins.ExcResult, () => ErrorStep());
                            ErrorStep();
                            ZBRTBurnNGRelease();
                            return;
                        }
                    });

                    if (!ex)
                    {
                        PState.SetEnd(ExcResult.Error, () => ErrorStep());
                        ErrorStep();
                        ZBRTBurnNGRelease();
                        return;
                    }
                }
                //Normal
                PState.SetEnd(ExcResult.Success);
            });
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
        private void ZBRTBurnNGRelease()
        {
            string mem_burn_result = "ZBRT_Burn_Result".GetPLCMem();
            SysLog.Add(LogLevel.Warning, $"{ZBRTBurnState.Title}:{SelectedResetOption}復歸...");

            Global.PLC.WriteOneData(mem_burn_result, PLCResetResult); 
            if (CurrentProduct != null)
                DispMain?.Invoke(() => CurrentProduct.TimeEnd = DateTime.Now);
            if(PLCResetResult != 0)
                ProcedureZBRTResetBurn();
            CurrentProduct = null;
        }
        #endregion
    }
}
