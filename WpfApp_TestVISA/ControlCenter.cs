using DeviceDB;
using Keysight.Visa;
using PLC;
using PropertyChanged;
using Support;
using Support.Data;
using Support.Logger;
using Support.Net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Convert = System.Convert;

namespace WpfApp_TestVISA
{
    [AddINotifyPropertyChangedInterface]
    public static class Global
    {
        public static Process? ProcessBurn { get; set; }
        public static Dictionary<string, Config> Configs { get; set; } = new();
        public static Dictionary<string, PLCAddr> PLCAddrs { get; set; } = new();
        public static Dictionary<string, Config> BATPath { get; set; } = new();
        internal static string KeysightDeviceIP = "";
        public static int PLCDelayMs { get; set; } = 50;
        public static int ModbusDelayMs { get; set; } = 500;

        public static CancellationTokenSource CtsTCP = new CancellationTokenSource();
        public static int PLCStationID { get; set; } = 0;
        public static PLCHandler PLC = new();
        public static string CurrentBurnBATPath { get; set; } ="";
        public static string? PowerMeterSerialPort { get; set; }
        public static SerialPort? ModbusPort{ get; set; }

        public static bool IsInitialized = false;

        internal static TCPCommand TcpCommand { get; set; }

        public static Model_Main? MMain { get; set; } = null;
        public static void Initialize()
        {
            _ = Task.Run(() =>
            {
                InitializeConfig();
                Task.Run(() =>
                {
                    "PLC連線".TryCatch(() =>
                    {
                        PLCStationID = Configs["PLCStationID"].Value.ToInt(0);
                        SysLog.Add(LogLevel.Info, $"PLC {PLCStationID} 連線中...");
                        var result = PLC.Open(PLCStationID, "");
                        if (result.IsSuccess)
                            SysLog.Add(LogLevel.Info, $"PLC {PLCStationID} 已連線: {result.ReturnCode}");
                        else
                            SysLog.Add(LogLevel.Error, $"PLC {PLCStationID} 連線失敗: {result.ReturnCode}");
                    });
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

                foreach (var path in BATPath.Values)
                    SysLog.Add(LogLevel.Info, $"{path.Title}:{path.Value}");
                KeysightDeviceIP = Configs["KeysightDeviceIP"].Value ?? "";
                Task.Run(async() =>
                {
                    SysLog.Add(LogLevel.Info, "頻譜儀連線中");
                    TcpCommand = new(KeysightDeviceIP, 5023);
                    await TcpCommand.ConnectAsync();
                    await TcpCommand.SendAndReceiveAsync("*IDN?\r\n");
                    await TcpCommand.ClearReadBuffer(CtsTCP.Token);
                });
                IsInitialized = true;
            });
            //*
            Task.Run(() =>
            {
                while (!IsInitialized)
                    Thread.Sleep(1000);
                while (true)
                {
                    Thread.Sleep(1000);
                    "產品檢測".TryCatch(() =>
                    {
                        short productType = PLC.ReadOneData("D3101").ReturnValue;
                        Model_Main.DispMain?.Invoke(() =>
                        {
                            if (MMain == null)
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
            byte[] iv = allData.Take(16).ToArray();
            aes.IV = iv;

            using MemoryStream ms = new(allData.Skip(16).ToArray());
            using CryptoStream cs = new(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
            using StreamReader sr = new(cs);
            return sr.ReadToEnd();
        }
    }
    public static class Extensions
    {
        public static string GetPLCMem(this string ID)
        {
            Global.PLCAddrs.TryGetValue(ID, out PLCAddr? PLCAddr);
            return PLCAddr?.Address ?? throw new Exception($"PLC位置{ID}:未定義");
        }
    }
}
