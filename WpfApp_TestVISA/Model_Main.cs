using PropertyChanged;
using Support;
using Support.Data;
using Support.Logger;
using Support.Wpf.Models;
using System.Windows.Input;
using Ivi.Visa;
using Ivi.Visa.FormattedIO;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using Support.Wpf;
using System.Windows.Media;
using System.Windows.Controls;
using PLC;
using WpfApp_TestVISA;
using System.Threading;
using Modbus.Device;
using System.IO.Ports;
using Modbus.Extensions.Enron;
using System.Diagnostics.Metrics;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Diagnostics;
namespace WpfApp_TestOmron
{
    [AddINotifyPropertyChangedInterface]
    public class Model_Main
    {
        public static Dispatcher? DispMain;
        public string IP_Port { get; set; } = "192.168.0.1:9600";
        public string WriteData { get; set; } = "*IDN?";
        public bool EnConnect { get; set; } = true;
        public string TextConnect { get; set; } = "連線";

        public static readonly string[] StrSteps = [
            "等待探針到位", "燒錄處理", "等待電測程序",
            "3V,uA 電表測試", "等待電表汽缸上升", "LED閃爍檢測", "頻譜儀天線強度測試",
            "5V,mA 電表測試", "DIO探針(指撥1)LED檢測", "開關探針(指撥1)LED檢測",
            "開蓋按鈕LED檢測", "磁簧汽缸LED檢測", "2.4V LED閃爍檢測", "完成並記錄資訊"
        ];
        private string PathBatBurn = "";
        public ObservableCollection<string> AssignedTests { get; set; } = ["燒錄bat呼叫", "頻譜儀天線測試", "3V-uA電表測試", "5V-mA電表測試", "LED 閃爍計數檢測"];

        public ObservableCollection<string> VISA_Devices = [];
        public string DeviceVISA { get; set; } = "";
        public ObservableCollection<Instruction> Instructions { get; set; } = [];
        public ObservableCollection<StepData> StepsData { get; set; } = [];
        public Dictionary<int, StepData?> MapSteps { get; set; } = [];
        public IMessageBasedSession? Session { get; set; }
        public ICommand Command_Refresh { get; set; }
        public ICommand Command_ConnectSwitch { get; set; }
        public ICommand Command_Write { get; set; }

        //public TcpClientApp TcpConnect { get; set; } = new();
        private bool isRefreshing = false;
        private bool IsConnected = false;
        MessageBasedFormattedIO? FormattedIO = null;
        public Model_Main()
        {
            ///重要: 使用這個函式庫需要先安裝 Library Suite
            ///https://www.keysight.com/tw/zh/lib/software-detail/computer-software/io-libraries-suite-downloads-2175637.html
            Command_Refresh = new RelayCommand<object>((obj) => {
                Task.Run(() =>
                {
                    "裝置刷新".TryCatch(() =>
                    {
                        if (isRefreshing) return;
                        isRefreshing = true;
                        var dev_list = GlobalResourceManager.Find("TCPIP?*inst?*INSTR");

                        DispMain?.Invoke(() => {
                            VISA_Devices = new(dev_list);
                            SysLog.Add(LogLevel.Info, $"已獲取裝置清單: {VISA_Devices.Count}個裝置");
                        });
                        isRefreshing = false;
                    });
                });
            });
            Command_ConnectSwitch = new RelayCommand<object>((obj) =>
            {
                $"裝置{TextConnect}".TryCatch(() => {
                    if (IsConnected)
                    {
                        Session?.Dispose();
                        FormattedIO = null;
                        Session = null;
                        EnConnect = true;
                        TextConnect = "連線";
                        IsConnected = false;
                        return;
                    }
                    else
                    {
                        EnConnect = false;
                        TextConnect = "連線中";
                        Task.Run(() =>
                        {
                            try
                            {
                                Session = GlobalResourceManager.Open(DeviceVISA) as IMessageBasedSession;
                                FormattedIO = new MessageBasedFormattedIO(Session);
                                DispMain?.Invoke(() =>
                                {
                                    EnConnect = true;
                                    TextConnect = "斷線";
                                    IsConnected = true;
                                });
                            }
                            catch (Exception)
                            {
                                SysLog.Add(LogLevel.Error, "連線超時");
                                EnConnect = true;
                                TextConnect = "連線";
                                IsConnected = false;
                            }
                        });
                    }
                    if (string.IsNullOrEmpty(DeviceVISA))
                        return;
                });

            });
            Command_Write = new RelayCommand<object>((obj) =>
            {
                "命令".TryCatch(() =>
                {
                    FormattedIO?.WriteLine(WriteData);
                    SysLog.Add(LogLevel.Success, $"已接收: {FormattedIO?.ReadLine()}");
                });
            });
            int sid = 1;
            foreach (var step in StrSteps)
            {
                StepData stepData = new() { ID = sid, ColorBrush = Brushes.LightBlue, Title = step };
                StepsData.Add(stepData);
                MapSteps.Add(sid, stepData);
                sid++;
            }
            Task.Run(() =>
            {
                Thread.Sleep(1000);
                Command_Refresh.Execute(null);
            });
        }
        public void ProcedureMain()
        {
            Instructions.Clear();
        }
    }

    public enum ExcResult
    {
        Success,
        Error,
        Abort,
        TimeOut,
        NotSupport
    }
    public enum Order
    {
        WaitPLCSiganl,
        SendPLCSignal,
        Burn,
        PLCSignalCount,
        SendModbus,
        WaitModbus,
        ReadModbusFloat,
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

        public object? Result { get; set; }
        private string InstructionMessage => $"指令{ID}-{InsOrder}-{Title}";
        public ExcResult Execute()
        {
            Result = null;
            try
            {
                switch (InsOrder)
                {
                    case Order.SendPLCSignal:
                        return SendPLC();
                    case Order.WaitPLCSiganl:
                        return WaitPLC();
                    case Order.Burn:
                        return BurnSequence();
                    case Order.PLCSignalCount:
                        return SignalCounter();
                    case Order.SendModbus:
                        return SendModbus();
                    case Order.ReadModbusFloat:
                        Result = ReadModbusFloat();
                        return ExcResult.Success;
                    case Order.WaitModbus:
                        return WaitModbus();
                }
            }catch(Exception)
            {
                return ExcResult.Error;
            }
            
            return ExcResult.NotSupport;
        }
        private ExcResult SendPLC()
        {
            if (Parameters.Length != 4)
                throw new Exception($"{InstructionMessage}: 錯誤的參數格式");
            string? Mem = Parameters[1] as string;
            if (Parameters[0] is not PLCHandler handler || string.IsNullOrEmpty(Mem) ||
                Parameters[2] is not short Value)
            throw new Exception($"{InstructionMessage}: 參數為空值");
            var result = handler.WriteOneData(Mem, Value);
            Thread.Sleep(GlobalConfig.PLCDelayMs);
            return (result.IsSuccess)? ExcResult.Success: ExcResult.Error;
        }

        private ExcResult WaitPLC()
        {
            if (Parameters.Length != 4)
                throw new Exception($"{InstructionMessage}: 錯誤的參數格式");
            string? Mem = Parameters[1] as string;
            short? targetValue = (short?)Parameters[2];
            short? TimeOutMs = (short?)Parameters[3];
            if ( Parameters[0] is not PLCHandler handler || string.IsNullOrEmpty(Mem))
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
                Thread.Sleep(GlobalConfig.PLCDelayMs);
            }
            return ExcResult.Abort; 
        }
        private ExcResult SignalCounter()
        {
            if (Parameters.Length != 4)
                throw new Exception($"{InstructionMessage}: 錯誤的參數格式");
            string? Mem = Parameters[1] as string;
            short? targetCount = (short?)Parameters[2];
            short? TimeOutMs = (short?)Parameters[3];
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
                        count++;
                    last_value = result.ReturnValue;
                }
                else return ExcResult.Error;
                if(count == targetCount)
                    return ExcResult.Success;
                Thread.Sleep(GlobalConfig.PLCDelayMs);
            }
            return ExcResult.Abort;
        }
        private ExcResult SendModbus()
        {
            if (Parameters.Length != 2)
                throw new Exception($"{InstructionMessage}: 錯誤的參數格式");

            SerialPort port = GlobalConfig.ModbusPort;
            var master = ModbusSerialMaster.CreateRtu(port);
            if (Parameters[0] is not ushort regAddr || // 0x001F; // DEC 31
                Parameters[1] is not ushort value)     // 1=Low, 2=High
                throw new Exception($"{InstructionMessage}: 參數為空值");
            port.Open();
            byte slaveId = 1;
            master.WriteSingleRegister(slaveId, regAddr, value);
            port.Close();
            return ExcResult.Success;
        }
        private float ReadModbusFloat()
        {
            if (Parameters.Length != 1)
                throw new Exception($"{InstructionMessage}: 錯誤的參數格式");
            ushort[] regs =[];
            SerialPort port = GlobalConfig.ModbusPort;
            
            var master = ModbusSerialMaster.CreateRtu(port);
            if (Parameters[0] is not ushort regAddr) // 0x0030(48): 低量程電流 //0x0032(50): 高量程電流
                throw new Exception($"{InstructionMessage}: 參數為空值");

            port.Open();
            byte slaveId = 1;
            regs = master.ReadHoldingRegisters(slaveId, regAddr, 2);
            port.Close();
            if(regs.Length == 4)
                return ConvertFloatFromRegisters(regs);

            return 0.0f;
        }
        private ExcResult WaitModbus()
        {
            if (Parameters.Length != 3)
                throw new Exception($"{InstructionMessage}: 錯誤的參數格式");
            ushort[] regs = [];
            SerialPort port = GlobalConfig.ModbusPort;

            var master = ModbusSerialMaster.CreateRtu(port);
            if (Parameters[0] is not ushort regAddr || //讀取目前使用量程狀態 (0x37)55 
                Parameters[1] is not ushort targetValue ||
                Parameters[2] is not short TimeOutMs)
                throw new Exception($"{InstructionMessage}: 參數為空值");
            port.Open();
            DateTime startT = DateTime.Now;
            while (!AbortSignal)
            {
                if (TimeOutMs > 0 && (DateTime.Now - startT).TotalMilliseconds > TimeOutMs)
                    return ExcResult.TimeOut;
                byte slaveId = 1;
                regs = master.ReadHoldingRegisters(slaveId, regAddr, 1);
                if (regs[0] == targetValue)
                    return ExcResult.Success;
                Thread.Sleep(GlobalConfig.ModbusDelayMs);
            }
            return ExcResult.Abort;

        }
        private ExcResult BurnSequence()
        {
            var process = new Process();
            process.StartInfo.FileName = GlobalConfig.BurnSequenceBAT;
            process.StartInfo.UseShellExecute = false;         // 必須為 false 才能重定向輸出
            process.StartInfo.RedirectStandardOutput = true;   // 重定向標準輸出
            process.StartInfo.RedirectStandardError = true;    // 重定向錯誤輸出
            process.StartInfo.CreateNoWindow = true;           // 不顯示 cmd 視窗

            string stdOutput = "";
            string stdError = "";

            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                    stdOutput += e.Data + Environment.NewLine;
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                    stdError += e.Data + Environment.NewLine;
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit(); // 等待執行完成

            string output = stdOutput;
            string error = stdError;
            if (output.Contains("ERROR") || error.Contains("ERROR"))
                return ExcResult.Error;
            if (output.Contains("Flashing completed successfully!") &&
                output.Contains("DONE"))
                return ExcResult.Success;
            return ExcResult.Error;
        }

        private static float ConvertFloatFromRegisters(ushort[] regs)
        {
            byte[] bytes = new byte[4];
            bytes[1] = (byte)(regs[0] & 0xFF);
            bytes[0] = (byte)(regs[0] >> 8);
            bytes[3] = (byte)(regs[1] & 0xFF);
            bytes[2] = (byte)(regs[1] >> 8);
            return BitConverter.ToSingle(bytes, 0);
        }
    }
}
