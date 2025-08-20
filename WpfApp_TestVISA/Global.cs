using PropertyChanged;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp_TestVISA
{
    [AddINotifyPropertyChangedInterface]
    public static class GlobalConfig
    {
        public static int PLCDelayMs { get; set; } = 100;
        public static int ModbusDelayMs { get; set; } = 500;

        public static string BurnSequenceBAT { get; set; } = @".\20250520_ZB_SPMG50_V2.0.1\ZB_SPMG50_V2_0_1_250520.BAT";

        public static SerialPort ModbusPort = new("COM3")
        {
            BaudRate = 9600,
            DataBits = 8,
            Parity = Parity.None,
            StopBits = StopBits.One
        };
        public static void InitializeConfig()
        {

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
}
