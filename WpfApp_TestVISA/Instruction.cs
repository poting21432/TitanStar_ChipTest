using DeviceDB;
using Modbus.Device;
using Modbus.Extensions.Enron;
using PLC;
using PropertyChanged;
using Support;
using Support.Data;
using Support.Logger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using WpfApp_TestOmron;

namespace WpfApp_TestVISA
{
    public enum Order
    {
        Custom,
        WaitPLCSiganl,
        SendPLCSignal,
        Burn,
        PLCSignalCount,
        SendModbus,
        WaitModbus,
        ReadModbusFloat,
        WaitModbusFloat,
    }
    [AddINotifyPropertyChangedInterface]
    public class Instruction(int ID, string Title, Order? InsOrder, params object[] Parameters)
    {
        public int? ID { get; set; } = ID;
        public string Title { get; set; } = Title;
        public object? Tag { get; set; }
        public Order? InsOrder { get; set; } = InsOrder;
        public bool AbortSignal { get; private set; }
        public object?[] Parameters { get; set; } = Parameters;
        public ExcResult ExcResult { get; set; } = ExcResult.NotSupport;
        public object? Result { get; set; }
        private string InstructionMessage => $"指令{ID}-{InsOrder}-{Title}";
        public Action<Instruction>? OnStart { get; set; }
        public Action<Instruction>? OnEnd { get; set; }

        public ExcResult Execute()
        {
            OnStart?.Invoke(this);
            ExcResult = ExcResult.NotSupport;
            Result = null;
            switch (InsOrder)
            {
                case Order.Custom:
                    ExcResult = ExcResult.Success;
                    break;
                case Order.SendPLCSignal:
                    ExcResult = SendPLC();
                    break;
                case Order.WaitPLCSiganl:
                    ExcResult = WaitPLC();
                    break;
                case Order.Burn:
                    ExcResult = BurnSequence();
                    break;
                case Order.PLCSignalCount:
                    ExcResult = SignalCounter();
                    break;
                case Order.SendModbus:
                    ExcResult = SendModbus();
                    break;
                case Order.ReadModbusFloat:
                    Result = ReadModbusFloat();
                    break;
                case Order.WaitModbus:
                    ExcResult = WaitModbus();
                    break;
                case Order.WaitModbusFloat:
                    ExcResult = WaitModbusFloat();
                    break;
            }
            OnEnd?.Invoke(this);
            return ExcResult;
        }
        private ExcResult SendPLC()
        {
            if (Parameters.Length != 3)
                throw new Exception($"{InstructionMessage}: 錯誤的參數格式");
            string? Mem = Parameters[1] as string;
            if (Parameters[0] is not PLCHandler handler || string.IsNullOrEmpty(Mem) ||
                Parameters[2] is not short Value)
                throw new Exception($"{InstructionMessage}: 參數為空值");
            var result = handler.WriteOneData(Mem, Value);
            Thread.Sleep(Global.PLCDelayMs);
            return (result.IsSuccess) ? ExcResult.Success : ExcResult.Error;
        }
        private ExcResult WaitPLC()
        {
            if (Parameters.Length != 4)
                throw new Exception($"{InstructionMessage}: 錯誤的參數格式");
            string? Mem = Parameters[1] as string;
            short? targetValue = (short?)Parameters[2];
            int? TimeOutMs = (int?)Parameters[3];
            if (Parameters[0] is not PLCHandler handler || string.IsNullOrEmpty(Mem))
                throw new Exception($"{InstructionMessage}: 參數為空值");

            DateTime startT = DateTime.Now;
            while (!AbortSignal)
            {
                if (TimeOutMs > 0 && (DateTime.Now - startT).TotalMilliseconds > TimeOutMs)
                    return ExcResult.TimeOut;

                var result = handler.ReadOneData(Mem);
                if (result.IsSuccess)
                {
                    if (result.ReturnValue == targetValue)
                        return ExcResult.Success;
                }
                else return ExcResult.Error;
                Thread.Sleep(Global.PLCDelayMs);
            }
            return ExcResult.Abort;
        }
        private ExcResult SignalCounter(bool IsLog = true)
        {
            if (Parameters.Length != 4)
                throw new Exception($"{InstructionMessage}: 錯誤的參數格式");
            string? Mem = Parameters[1] as string;
            short targetCount = (short)Parameters[2]!;
            int TimeOutMs = Parameters[3].ToInt()!;
            if (Parameters[0] is not PLCHandler handler || string.IsNullOrEmpty(Mem))
                throw new Exception($"{InstructionMessage}: 參數為空值");
            int count = 0;
            short last_value = 0;

            DateTime startT = DateTime.Now;
            while (!AbortSignal)
            {
                if (TimeOutMs > 0 && (DateTime.Now - startT).TotalMilliseconds > TimeOutMs)
                    return ExcResult.TimeOut;
                var result = handler.ReadOneData(Mem);
                if (result.IsSuccess)
                {
                    if (result.ReturnValue == 0 && last_value == 1)
                    {
                        count++;
                        if (IsLog)
                            SysLog.Add(LogLevel.Info, $"{Mem}: v^ * {count}");
                    }
                    last_value = result.ReturnValue;
                }
                else return ExcResult.Error;
                if (count == targetCount)
                    return ExcResult.Success;
                Thread.Sleep(Global.PLCDelayMs);
            }
            return ExcResult.Abort;
        }
        private ExcResult SendModbus()
        {
            if (Global.ModbusPort == null)
            {
                SysLog.Add(LogLevel.Error, "電表序列埠未設定");
                return ExcResult.Error;
            }
            if (Parameters.Length != 2)
                throw new Exception($"{InstructionMessage}: 錯誤的參數格式");
            if (Parameters[0] is not ushort regAddr || // 0x001F; // DEC 31
                Parameters[1] is not ushort value)     // 1=Low, 2=High
                throw new Exception($"{InstructionMessage}: 參數為空值");
            SerialPort port = Global.ModbusPort ?? throw new Exception($"電表序列埠未設定");

            if (port.IsOpen)
                port.Close();
            port.Open();
            port.WriteTimeout = 1000;
            port.ReadTimeout = 1000;
            byte slaveId = 1;
            var master = ModbusSerialMaster.CreateRtu(port);
            master.WriteMultipleRegisters(slaveId, regAddr, [value]);
            port.Close();
            return ExcResult.Success;
        }
        private float ReadModbusFloat()
        {
            if (Global.ModbusPort == null)
            {
                SysLog.Add(LogLevel.Error, "電表序列埠未設定");
                ExcResult = ExcResult.Error;
                return (float)0.0;
            }
            if (Parameters.Length != 1)
                throw new Exception($"{InstructionMessage}: 錯誤的參數格式");
            ushort[] regs = [];
            SerialPort port = Global.ModbusPort;

            var master = ModbusSerialMaster.CreateRtu(port);
            if (Parameters[0] is not ushort regAddr) // 0x30(48): 低量程電流 //0x32(50): 高量程電流
                throw new Exception($"{InstructionMessage}: 參數為空值");

            if (port.IsOpen)
                port.Close();
            port.Open();
            port.WriteTimeout = 1000;
            port.ReadTimeout = 1000;
            byte slaveId = 1;
            regs = master.ReadHoldingRegisters(slaveId, regAddr, 2);
            port.Close();
            if (regs.Length == 2)
            {
                ExcResult = ExcResult.Success;
                return ConvertFloatFromRegisters(regs);
            }
            else
            {
                ExcResult = ExcResult.Error;
                return 0.0f;
            }
        }
        private ExcResult WaitModbus()
        {
            if (Global.ModbusPort == null)
            {
                SysLog.Add(LogLevel.Error, "電表序列埠未設定");
                return ExcResult.Error;
            }
            if (Parameters.Length != 3)
                throw new Exception($"{InstructionMessage}: 錯誤的參數格式");
            ushort[] regs = [];
            SerialPort port = Global.ModbusPort;

            var master = ModbusSerialMaster.CreateRtu(port);
            if (Parameters[0] is not ushort regAddr || //讀取目前使用量程狀態 (0x37)55 
                Parameters[1] is not ushort targetValue ||
                Parameters[2] is not int TimeOutMs)
                throw new Exception($"{InstructionMessage}: 參數為空值");
            if (port.IsOpen)
                port.Close();
            port.Open();
            port.WriteTimeout = 1000;
            port.ReadTimeout = 1000;
            DateTime startT = DateTime.Now;
            while (!AbortSignal)
            {
                if (TimeOutMs > 0 && (DateTime.Now - startT).TotalMilliseconds > TimeOutMs)
                    return ExcResult.TimeOut;
                byte slaveId = 1;
                regs = master.ReadHoldingRegisters(slaveId, regAddr, 1);
                if (regs[0] == targetValue)
                    return ExcResult.Success;
                Thread.Sleep(Global.ModbusDelayMs);
            }
            return ExcResult.Abort;
        }
        private ExcResult WaitModbusFloat()
        {
            if (Global.ModbusPort == null)
            {
                SysLog.Add(LogLevel.Error, "電表序列埠未設定");
                ExcResult = ExcResult.Error;
                return ExcResult.Error;
            }
            if (Parameters.Length != 4)
                throw new Exception($"{InstructionMessage}: 錯誤的參數格式");

            float volt = 0.0f;
            ushort[] regs = [];
            SerialPort port = Global.ModbusPort;
            var master = ModbusSerialMaster.CreateRtu(port);
            if (Parameters[0] is not ushort regAddr) // 0x30(48): 低量程電流 //0x32(50): 高量程電流
                throw new Exception($"{InstructionMessage}: 參數為空值");
            if (Parameters[1] is not float minV)
                throw new Exception($"{InstructionMessage}: 參數為空值");
            if (Parameters[2] is not float maxV)
                throw new Exception($"{InstructionMessage}: 參數為空值");
            if (Parameters[3] is not int timeOut)
                throw new Exception($"{InstructionMessage}: 參數為空值");
            if (port.IsOpen)
                port.Close();
            port.Open();
            port.WriteTimeout = 1000;
            port.ReadTimeout = 1000;
            DateTime t_start = DateTime.Now;

            while((DateTime.Now - t_start).TotalMilliseconds < timeOut)
            {
                Thread.Sleep(500);
                byte slaveId = 1;
                regs = master.ReadHoldingRegisters(slaveId, regAddr, 2);
                if (regs.Length == 2)
                {
                    ExcResult = ExcResult.Success;
                    volt = ConvertFloatFromRegisters(regs);
                    Result = volt;
                    if (volt < maxV && volt > minV)
                    {
                        port.Close();
                        return ExcResult.Success;
                    }
                }
                else
                {
                    ExcResult = ExcResult.Error;
                    volt = float.NaN; 
                    Result = null;
                }
            }
            port.Close();
            return ExcResult.TimeOut;
        }
        private ExcResult BurnSequence()
        {
            SysLog.Add(LogLevel.Info, "燒錄中(TimeOut:30s)");
            if (Parameters.Length != 1)
                throw new Exception($"{InstructionMessage}: 錯誤的參數格式");
            string BurnBAT = Parameters[0]?.ToString() ?? "";
            Global.ProcessBurn = new Process();
            Global.BATPath.TryGetValue(BurnBAT, out Config? config);
            if (string.IsNullOrEmpty(config?.Value))
            {
                SysLog.Add(LogLevel.Error, $"未設定{BurnBAT} BAT路徑");
                return ExcResult.Error;
            }
            Global.CurrentBurnBATPath = config?.Value ?? "";
            if (!Path.Exists(Global.CurrentBurnBATPath))
            {
                SysLog.Add(LogLevel.Error, $"{BurnBAT} BAT路徑 {Global.CurrentBurnBATPath}");
                return ExcResult.Error;
            }
            ExcResult result = ExcResult.Success;
            Global.ProcessBurn.StartInfo.FileName = Global.CurrentBurnBATPath;
            Global.ProcessBurn.StartInfo.UseShellExecute = false;         // 必須為 false 才能重定向輸出
            Global.ProcessBurn.StartInfo.RedirectStandardOutput = true;   // 重定向標準輸出
            Global.ProcessBurn.StartInfo.RedirectStandardError = true;    // 重定向錯誤輸出
            Global.ProcessBurn.StartInfo.CreateNoWindow = true;           // 不顯示 cmd 視窗

            string stdOutput = "";
            string stdError = "";

            Global.ProcessBurn.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    stdOutput += e.Data + Environment.NewLine;
                    if (e.Data.Contains("ERROR"))
                        SysLog.Add(LogLevel.Error, $"{e.Data}");
                    else if (!string.IsNullOrWhiteSpace(e.Data))
                        SysLog.Add(LogLevel.Info, $"{e.Data}");
                }
            };

            Global.ProcessBurn.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    stdError += e.Data + Environment.NewLine;
                    if (!string.IsNullOrWhiteSpace(e.Data))
                        SysLog.Add(LogLevel.Error, $"{e.Data}");
                }
            };

            Global.ProcessBurn.Start();
            Global.ProcessBurn.BeginOutputReadLine();
            Global.ProcessBurn.BeginErrorReadLine();
            bool isComplete = Global.ProcessBurn.WaitForExit(30000); // 等待執行完成
            string output = stdOutput;
            string error = stdError;
            if (!isComplete)
            {
                SysLog.Add(LogLevel.Error, "燒錄超時:30s");
                result = ExcResult.TimeOut;
            }
            else if (output.Contains("ERROR") || error.Contains("ERROR"))
            {
                SysLog.Add(LogLevel.Error, "燒錄失敗");
                result = ExcResult.Error;
            }
            else SysLog.Add(LogLevel.Success, "燒錄完成");
            Global.ProcessBurn = null;
            return result;
        }

        private static float ConvertFloatFromRegisters(ushort[] regs)
        {
            byte[] bytes = new byte[4];
            bytes[3] = (byte)(regs[0] >> 8);
            bytes[2] = (byte)(regs[0] & 0xFF);
            bytes[0] = (byte)(regs[1] >> 8);
            bytes[1] = (byte)(regs[1] & 0xFF);
            return BitConverter.ToSingle(bytes, 0);
        }
    }
    [AddINotifyPropertyChangedInterface]
    public class ProcedureState(string Title)
    {
        public ExcResult ExcResult { get; set; } = ExcResult.Null;
        public string Title { get; set; } = Title;
        public SolidColorBrush BrushState { get; set; } = Brushes.Transparent;
        public DateTime? TStart { get;set; }
        public int ProductCount { get; set; } = 0;

        private bool isModeStep = false;
        public bool IsModeStep { get => isModeStep;
            set 
            {
                if (!isModeStep && value)
                    SysLog.Add(LogLevel.Warning, "步進模式啟用");
                else if(isModeStep && !value)
                    SysLog.Add(LogLevel.Warning, "步進模式解除");
                isModeStep = value;
            } 
        }
        public bool IsEnAutoNG { get; set; } = true;
        public bool IsBusy { get; set; } = false;
        public bool SignalNext { get;set; } = false;
        public bool IsStop { get; set; } = false;
        public bool IsReseting { get; set; } = false;
        public bool IsCompleted { get; set; } = false;

        public void ResetState(string Title)
        {
            this.Title = Title;
            ProductCount = 0;
        }
        public void SetStart()
        {
            Model_Main.DispMain?.Invoke(() =>
            {
                BrushState = Brushes.Blue;
                TStart = DateTime.Now;
                IsBusy = true;
                SignalNext = false;
                IsStop = false;
                IsCompleted = false;
            });
        }
        public void SetEnd(ExcResult excR)
        {
            double t = (DateTime.Now - TStart)?.TotalSeconds ?? double.NaN;
            Model_Main.DispMain?.Invoke(() =>
            {
                string fstr = ((excR) switch
                {
                    ExcResult.Abort => "手動強制",
                    ExcResult.Success => "正常",
                    _ => "異常",
                });
                LogLevel logLevel = ((excR) switch
                {
                    ExcResult.Abort => LogLevel.Warning,
                    ExcResult.Success => LogLevel.Info,
                    _ => LogLevel.Error,
                });
                BrushState = ((excR) switch
                {
                    ExcResult.Abort => Brushes.Red,
                    ExcResult.Success => Brushes.GreenYellow,
                    _ => Brushes.Red,
                });
                SysLog.Add(logLevel, $"程序{fstr}結束，花費時間{t:F2}秒");
                IsBusy = false;
                SignalNext = false;
                IsStop = false;
                IsCompleted = true;
            });
        }
    }
}
