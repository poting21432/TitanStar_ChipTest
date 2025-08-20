using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.ComponentModel;
using System.ServiceModel;
namespace PLC
{
#if OffPLC
#else
    using UtlType = ActUtlType64Lib.ActUtlType64Class;
    using SupportMsg = ActSupportMsg64Lib.ActSupportMsg64Class;
#endif
    public class PLCHandler
    {
#region Private Member
        private int logicalNum = -1;
        private string password = "";
#if OffPLC
#else
        private readonly UtlType lpcom_ReferencesUtlType;
        //used for intialize PLC
#endif

#endregion

#region Public Member      
        public int LogicalStationNumber => logicalNum;
        public string Password => password;
        public bool IsOpen { get; set; } = false;
#endregion

#region Public Method

        public PLCHandler()
        {
#if OffPLC
#else
            lpcom_ReferencesUtlType = new UtlType();
#endif
        }

        /// <summary>
        /// Connect to PLC.
        /// </summary>     
        public PLCResult Open(int logicalNum, string password)
#if OffPLC
            => new(-1);
#else
        {
            lpcom_ReferencesUtlType.ActLogicalStationNumber = this.logicalNum = logicalNum;
            lpcom_ReferencesUtlType.ActPassword = this.password = password;
            int result = lpcom_ReferencesUtlType.Open();
            return new PLCResult(result);
        }
#endif
        /// <summary>
        /// Disconnect to PLC.
        /// </summary>
        public PLCResult Close()
#if OffPLC
            => new(-1);
#else
            => ((Func<PLCResult>)(() => new PLCResult(lpcom_ReferencesUtlType.Close()))).TryCatch();
#endif

        /// <summary>
        /// Read random data.
        /// </summary>      
        public PLCResult ReadRandomData(string[] deviceNameRandom)
#if OffPLC
            => new(-1);
#else
            => ((Func<PLCResult>)(() => {
                short[] returnValue = new short[deviceNameRandom.Length];

                string szDeviceName = String.Join("\n", deviceNameRandom);
                return new PLCResult(lpcom_ReferencesUtlType.ReadDeviceRandom2(
                     szDeviceName, deviceNameRandom.Length, out returnValue[0])){
                    ReturnValues = returnValue
                };
            })).TryCatch();
#endif

        /// <summary>
        /// Write random data.
        /// </summary>     
        public PLCResult WriteRandomData(string[] deviceNameRandom, short[] value)
#if OffPLC
            => new(-1);
#else
            => ((Func<PLCResult>)(() => {

                string szDeviceName = String.Join("\n", deviceNameRandom);
                return new PLCResult(lpcom_ReferencesUtlType.WriteDeviceRandom2(
                     szDeviceName, deviceNameRandom.Length, ref value[0]));
            })).TryCatch();
#endif

        /// <summary>
        /// Read block data.
        /// </summary>        
        public PLCResult ReadBlockData(string deviceNameRandom, int dataCount)
#if OffPLC
            => new(-1);
#else
            => ((Func<PLCResult>)(() => {
                string szDeviceName = String.Join("\n", deviceNameRandom);
                short[] returnValue = new short[dataCount];
                return new PLCResult(lpcom_ReferencesUtlType.ReadDeviceBlock2(
                     szDeviceName, dataCount, out returnValue[0]))
                {
                    ReturnValues = returnValue
                };
            })).TryCatch();
#endif

        /// <summary>
        /// Read one data.
        /// </summary>        
        public PLCResult ReadOneData(string deviceNameRandom)
#if OffPLC
            => new(-1);
#else
            => ReadRandomData(new string[] { deviceNameRandom });
#endif

        /// <summary>
        /// Write random data.
        /// </summary>     
        public PLCResult WriteBlockData(string deviceNameRandom, short[] value)
#if OffPLC
            => new(-1);
#else
            => ((Func<PLCResult>)(() =>
                new PLCResult(lpcom_ReferencesUtlType.WriteDeviceBlock2(
                     deviceNameRandom, value.Length,ref value[0]))
            )).TryCatch();
#endif

        /// <summary>
        /// Write random data.
        /// </summary>     
        public PLCResult WriteOneData(string deviceNameRandom, short value)
#if OffPLC
            => new(-1);
#else
            => WriteRandomData(new string[] { deviceNameRandom }, new short[] { value });
#endif
#endregion

    }
    public class PLCResult
    {
        public Exception? Exception { get; set; } = null;
        public bool IsSuccess => (ReturnCode == 0) && Exception == null;
        public int ReturnCode { get; set; }
        //public string ErrorMsg => ReturnCode.GetErrorMessage();

        public short[] ReturnValues { get; set; } = Array.Empty<short>();
        public short ReturnValue => (ReturnValues.Length > 0) ? ReturnValues[0] : (short)-1;
        public PLCResult(int returnCode)
        {
            ReturnCode = returnCode;
        }
    }
    public static class PLCExtendMethods
    {
#if OffPLC
#else  
private static readonly SupportMsg lpcom_ReferencesMsg = new SupportMsg();
#endif
        public static PLCResult TryCatch(this Func<PLCResult> func)
        {
            try
            {
                return func?.Invoke() ?? new PLCResult(-1);
            }
            //Exception processing			
            catch (Exception exception)
            {
                return new PLCResult(-2) { Exception = exception };
            }
        }
        public static string GetErrorMessage(this int ReturnCode)
        {
#if OffPLC
            if(ReturnCode == -1)
                return "OffPLC Mode";
            if (ReturnCode == -2)
                return "Exception";
            else
                return "";
#else
            try
            {
                lpcom_ReferencesMsg.GetErrorMessage(ReturnCode, out string ErrorMessage);
                return ErrorMessage;
                //return "";
            }

            // Exception processing			
            catch (Exception exception)
            {
                return exception.Message;
            }
#endif
        }
    }
}
