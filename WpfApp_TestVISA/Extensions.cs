using Support.Files;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp_TestOmron
{
    public static class Extensions
    {
        public static byte[] ConcatMultiple(this byte[] startArray, params byte[][] arrays)
        {
            using MemoryStream ms = new();
            ms.Write(startArray, 0, startArray.Length);
            foreach (var arr in arrays)
                ms.Write(arr, 0, arr.Length);
            return ms.ToArray();
        }
        public static Dictionary<byte, StatusCode> StatusCode = [];

        public static StatusCode? GetStatusCode(this byte code)
        {
            StatusCode.TryGetValue(code, out StatusCode? status);
            return status;
        }

        public static string GetHexFormat(this string MemortyType, int StartMemory, int ShiftBit) =>
            $"{MemortyType}{(StartMemory + ShiftBit / 16):D3}.{ShiftBit % 16:D2}";
        static Extensions()
        {
            if(File.Exists("./StatusCode.csv"))
            {
                List<StatusCode> codes = [];
                try
                {

                    codes.FromCSV("./StatusCode.csv");
                    StatusCode = codes.ToDictionary(x => x.Code);
                }catch(Exception e)
                {

                }
            }
        }
    }
    public class StatusCode
    {
        /// <summary>
        /// The code from the PLC
        /// </summary>
        public byte Code { get; set; }

        /// <summary>
        /// The message associated with the code
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Specifies whether this code represents and error
        /// </summary>
        public bool IsError { get; set; }
    }
}
