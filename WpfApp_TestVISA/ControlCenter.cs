using DeviceDB;
using PLC;
using PropertyChanged;
using Support;
using Support.Data;
using Support.Logger;
using Support.Net;
using Support.ThreadHelper;
using Support.Wpf.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using Convert = System.Convert;

namespace WpfApp_TitanStar_TestPlatform
{
    [AddINotifyPropertyChangedInterface]
    public static class Global
    {
        public static string AppVer = "v1.0.1";
        public static Process? ProcessBurn { get; set; }
        public static Dictionary<string, Config> Configs { get; set; } = [];
        public static Dictionary<string, PLCAddr> PLCAddrs { get; set; } = [];
        public static Dictionary<string, Config> BATPath { get; set; } = [];
        internal static string KeysightDeviceIP = "";
        internal static int KeysightDevicePort = 5023;
        public static int PLCDelayMs { get; set; } = 50;
        public static int ModbusDelayMs { get; set; } = 500;

        public static CancellationTokenSource CtsTCP = new CancellationTokenSource();
        public static int PLCStationID { get; set; } = 0;
        internal static PLCHandler PLC = new();
        public static string CurrentBurnBATPath { get; set; } ="";
        public static string? PowerMeterSerialPort { get; set; }
        public static SerialPort? ModbusPort{ get; set; }

        public static bool IsInitialized = false;

        internal static TCPCommand? TcpCommand { get; set; }
        public static Model_Main? MMain { get; set; } = null;

        public static void Initialize(Action? OnInitialized)
       {
            _ = Task.Run(() =>
            {
                InitializeConfig();
                Task.Run(() =>
                {
                    DeviceDisplay? PLCDisplay = null;
                    MMain?.DevStateMap.TryGetValue("PLC", out PLCDisplay);
                    if (PLCDisplay == null) return;
                    PLCDisplay.CommandReconnect = new RelayCommand<object>((obj) =>
                    {
                        Task.Run(() =>
                        {
                            "PLC連線".TryCatch(() =>
                            {
                                PLCStationID = Configs["PLCStationID"].Value.ToInt(0);
                                PLCDisplay.SetState(DeviceState.Connecting);
                                SysLog.Add(LogLevel.Info, $"PLC {PLCStationID} 連線中...");
                                var result = PLC.Open(PLCStationID, "");
                                if (result.IsSuccess)
                                {
                                    PLCDisplay.SetState(DeviceState.Connected);
                                    SysLog.Add(LogLevel.Info, $"PLC {PLCStationID} 已連線: {result.ReturnCode}");
                                }
                                else
                                {
                                    PLCDisplay.SetState(DeviceState.Error);
                                    SysLog.Add(LogLevel.Error, $"PLC {PLCStationID} 連線失敗: {result.ReturnCode}");
                                }
                            });
                        });
                    });
                    PLCDisplay.CommandReconnect?.Execute(null);
                });
                PowerMeterSerialPort = Configs["PowerMeterSerialPort"].Value;
                ModbusPort = new(PowerMeterSerialPort)
                {
                    BaudRate = 9600,
                    DataBits = 8,
                    Parity = Parity.None,
                    StopBits = StopBits.One
                };
                SysLog.Add(LogLevel.Info, $"使用電表序列埠:{PowerMeterSerialPort}");

                DeviceDisplay? PMDisplay = null;
                MMain?.DevStateMap.TryGetValue("電表", out PMDisplay);
                if(PMDisplay!=null)
                    PMDisplay.CommandReconnect = new RelayCommand<object>((obj) =>
                    {
                        Task.Run(()=> MMain?.Command_SetPowerMeter_High?.Execute(null));
                    });
                PMDisplay?.CommandReconnect?.Execute(null);

                foreach (var path in BATPath.Values)
                    SysLog.Add(LogLevel.Info, $"{path.Title}:{path.Value}");

                Configs.TryGetValue("KeysightDeviceIP", out Config? c_devIP);
                KeysightDeviceIP = c_devIP?.Value ?? "";
                Configs.TryGetValue("KeysightDevicePort", out Config? c_devPort);
                KeysightDevicePort = c_devPort?.Value?.ToInt() ?? 5023;
                
                DeviceDisplay? RFDisplay = null;
                MMain?.DevStateMap.TryGetValue("頻譜儀", out RFDisplay);
                if (RFDisplay != null)
                    RFDisplay.CommandReconnect = new RelayCommand<object>((obj) =>
                    {
                        Task.Run(async () =>
                        {
                            await LinkSignalAnalyzer();
                            await Model_Main.PrepareRF();
                            if(TcpCommand !=null)
                            {
                                string[] recv1 = await TcpCommand.SendAndReceiveTokenAsync(":ABORt\r\n", "SCPI", CtsTCP.Token);
                                string[] recv2 = await TcpCommand.SendAndReceiveTokenAsync(":TRIG:TXP:SOUR IMM\r\n", "SCPI", CtsTCP.Token);
                                string[] recv3 = await TcpCommand.SendAndReceiveTokenAsync(":FETC:BPOW?\r\n", "SCPI", CtsTCP.Token);
                            }
                        });

                    });
                RFDisplay?.CommandReconnect?.Execute(null);
                IsInitialized = true;
                OnInitialized?.Invoke();
            });
            //* 
            Task.Run(() => //產品自動切換
            {
                while (!IsInitialized)
                    Thread.Sleep(1000);
                string mem_pType = "ProductType".GetPLCMem();
                while (true)
                {
                    Thread.Sleep(1000);
                    "產品檢測".TryCatch(() =>
                    {
                        short productType = PLC.ReadOneData(mem_pType).ReturnValue;
                        Model_Main.DispMain?.Invoke(() =>
                        {
                            if (MMain == null || MMain.IsManualMode)
                                return;
                            if (productType == 1 && MMain!.SelectedProductType !="G51")
                            {
                                SysLog.Add(LogLevel.Warning, "PLC產品切換為G51");
                                MMain.SelectedProductType = "G51";
                            }
                            if (productType == 2 && MMain!.SelectedProductType != "ZBRT")
                            {
                                SysLog.Add(LogLevel.Warning, "PLC產品切換為ZBRT");
                                MMain.SelectedProductType = "ZBRT";
                            }
                        });
                    });
                }
            });//*/

            Task.Run(() => //自動狀態到位檢查
            {
                while (!IsInitialized)
                    Thread.Sleep(500);
                string memReady_G51 = "G51_Signal_Ready".GetPLCMem();
                string memReady_ZBRT = "ZBRT_Signal_Ready".GetPLCMem();
                string memReady_BG51 = "G51_Burn_Ready".GetPLCMem();
                string memReady_BZBRT = "ZBRT_Burn_Ready".GetPLCMem();
                while (true)
                {
                    Thread.Sleep(500);
                    if (MMain == null || MMain.IsManualMode)
                        continue;
                    if (MMain.SelectedProductType == "G51")
                    {
                        short value_BG51 = PLC.ReadOneData(memReady_BG51).ReturnValue;
                        short value_G51 = PLC.ReadOneData(memReady_G51).ReturnValue;

                        if (value_BG51 == 1 && !MMain.G51BurnState.IsBusy && !MMain.G51BurnState.IsReseting)
                            MMain.ProcedureBurn_G51();
                        else if (value_G51 == 1 && !MMain.G51TestState.IsBusy && !MMain.G51TestState.IsReseting)
                            MMain.ProcedureTest_G51();
                    }
                    else if (MMain.SelectedProductType == "ZBRT")
                    {
                        short value_BZBRT = PLC.ReadOneData(memReady_BZBRT).ReturnValue;
                        short value_ZBRT = PLC.ReadOneData(memReady_ZBRT).ReturnValue;
                        if (value_BZBRT == 1 && !MMain.ZBRTBurnState.IsBusy && !MMain.ZBRTBurnState.IsReseting)
                            MMain.ProcedureBurn_ZBRT();
                        else if (value_ZBRT == 1 && !MMain.ZBRTTestState.IsBusy && !MMain.ZBRTTestState.IsReseting)
                            MMain.ProcedureTest_ZBRT();
                    }
                }
            });
            Task.Run(() => //初始化PLC位置表
            {
                while (!IsInitialized || MMain == null)
                    Thread.Sleep(500);
                Model_Main.DispMain?.Invoke(() =>
                {
                    MMain.PLCAddrData.Clear();
                    foreach (var addr in PLCAddrs.Values)
                    {
                        PLCData data = new() { Id = addr.Id, Address = addr.Address, Title = addr.Title };
                        MMain.PLCAddrData.Add(data);
                    }
                });
            });
            Task.Run(() => //PLCIO同步
            {
                DeviceDisplay? PLCDisplay = null;
                MMain?.DevStateMap.TryGetValue("PLC", out PLCDisplay);
                string[] PLCAddrList = [];
                while (!IsInitialized || MMain == null || MMain.PLCAddrData.Count != PLCAddrs.Values.Count)
                    Thread.Sleep(500);

                PLCAddrList = [.. MMain.PLCAddrData.Select(x => x.Address ?? "")];
                "PLC同步".TryCatch(() =>
                {
                    while (true)
                    {
                        Thread.Sleep(500);
                        if(PLCDisplay?.State != DeviceState.Error &&
                           PLCDisplay?.State != DeviceState.Connecting)
                            PLCDisplay?.SetState(DeviceState.Transporting);
                        var result = PLC.ReadRandomData(PLCAddrList);
                        if (!result.IsSuccess)
                        {
                            if (PLCDisplay?.State != DeviceState.Error)
                                SysLog.Add(LogLevel.Error, "PLC通訊異常，請檢查網路連線");
                            PLCDisplay?.SetState(DeviceState.Error);
                            continue;
                        }
                        PLCDisplay?.SetState(DeviceState.Connected);
                        Model_Main.DispMain?.Invoke(() =>
                        {
                            
                            using (Dispatcher.CurrentDispatcher.DisableProcessing())
                            {
                                for (int i = 0; i < MMain.PLCAddrData.Count; i++)
                                {
                                    if (i < result.ReturnValues.Length)
                                        MMain.PLCAddrData[i].Status = result.ReturnValues[i];
                                }
                            }
                        });
                    }
                });
            });
        }
        internal static async Task<bool>LinkSignalAnalyzer()
        {
            DeviceDisplay? RFDisplay = null;
            MMain?.DevStateMap.TryGetValue("頻譜儀", out RFDisplay);
            if (TcpCommand != null && (TcpCommand.IsConnected ?? false))
                return true;
            RFDisplay?.SetState(DeviceState.Connecting);
            SysLog.Add(LogLevel.Info, "頻譜儀連線中");
            TcpCommand = new(KeysightDeviceIP, KeysightDevicePort);
            bool isC = await TcpCommand.ConnectAsync(CtsTCP.Token);
            if(isC)
            {
                RFDisplay?.SetState(DeviceState.Connected);
                Thread.Sleep(500);
                string[] recv = await TcpCommand.SendAndReceiveTokenAsync("*IDN?\r\n", "SCPI", CtsTCP.Token);
                string[] info = recv[0].Replace("\n", "").Trim().Split("\r");
                if(info.Length >= 3)
                    SysLog.Add(LogLevel.Info, $"頻譜儀型號:{info[2]}");
                
            }
            else RFDisplay?.SetState(DeviceState.Error);
            return isC;
        }
        static void InitializeConfig()
        {
            SysLog.Add(LogLevel.Info, "讀取設定檔...");
            using ConifgsDBContext ConfigsDB = new();
            Configs = ConfigsDB.Configs.ToDictionary(x => x.Id);
            PLCAddrs = ConfigsDB.PlCAddrs.ToDictionary(x => x.Id);
            SysLog.Add(LogLevel.Info, "已讀取設定檔");

            BATPath = Configs.Values.Where(x => x.Group == "PathBAT").ToDictionary(x => x.Id);
        }

        // GetKey: 從程式中多層運算組合出密鑰（非明文）
        public static byte[] GetKey()
        {
            byte[] obfuscated = ":5V;?;T5:%\\5:;R1"u8.ToArray();

            byte[] key = new byte[obfuscated.Length];

            for (int i = 0; i < obfuscated.Length; i++)
            {
                // 混合 XOR 與位移（與某一 magic number）
                key[i] = (byte)((obfuscated[i] ^ 0x5A) - ((i * 3) % 7));
            }

            return key; // AES-128 需要 16 byte
        }

        public static string EcStr(string plainText)
        {
            byte[] key = GetKey();

            using Aes aes = Aes.Create();
            aes.Key = key;
            aes.GenerateIV();

            using MemoryStream ms = new();
            ms.Write(aes.IV, 0, aes.IV.Length); // 將 IV 寫入檔案前面

            using CryptoStream cs = new(ms, aes.CreateEncryptor(), CryptoStreamMode.Write);
            using StreamWriter sw = new(cs);
            sw.Write(plainText);

            return Convert.ToBase64String(ms.ToArray());
        }
        public static string DeStr(string enc)
        {
            byte[] allData = Convert.FromBase64String(enc);
            byte[] key = GetKey();

            using Aes aes = Aes.Create();
            aes.Key = key;

            // 取出前 16 byte 為 IV
            byte[] iv = [.. allData.Take(16)];
            aes.IV = iv;

            using MemoryStream ms = new(allData.Skip(16).ToArray());
            using CryptoStream cs = new(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
            using StreamReader sr = new(cs);
            return sr.ReadToEnd();
        }
    }
    public static partial class Extensions
    {
        public static string GetPLCMem(this string ID)
        {
            Global.PLCAddrs.TryGetValue(ID, out PLCAddr? PLCAddr);
            return PLCAddr?.Address ?? throw new Exception($"PLC位置{ID}:未定義");
        }
    }
}
