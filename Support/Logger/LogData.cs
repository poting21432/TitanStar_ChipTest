using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Data;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Documents;
using Support.Wpf;
using IniParser.Model;
using System.Timers;
using Support.IniHelper;
using System.ComponentModel;

namespace Support.Logger
{
    public enum LogLevel {
        [Description("訊息")]
        Info,
        [Description("錯誤")]
        Error,
        [Description("警告")]
        Warning,
        [Description("成功")]
        Success
    }
    public struct LogData
    {
        public DateTime Time;
        public LogLevel Level;
        public string Data;
        public LogData(DateTime time, LogLevel level, string data)
        {
            Time = time;
            Level = level;
            Data = data;
        }
        public string LogString(string logTimeFormat) => 
            string.Format("{0},{1},{2}", DateTime.Now.ToString(logTimeFormat), Level.ToString(), Data);
        public string UIString(string UITimeFormat) =>
           string.Format("{0}   {1}", DateTime.Now.ToString(UITimeFormat), Data);
        public void WriteLog(string logFilePath,string logTimeFormat)
        {
            string logPath = string.Format(@".\Log\{0}", logFilePath);
            try {
                using (StreamWriter sw = new StreamWriter(logPath, true, Encoding.UTF8))
                {
                    string data = LogString(logTimeFormat);
                    sw.WriteLine(data);
                    sw.Close();
                }
            }
            catch (Exception) {
                throw;
            }
        }
        
    }
    public class Logger : IniReadWrite, IDisposable
    {
        public const string IniFilePath = @".\log.ini";
        public Action<LogLevel, string>? FileEvent;
        public Action<LogLevel, string>? UISyncEvent;
        public string DirectoryPath { get; set; }  = @".\Log";
        public string FileNameFormat { get; set; } = @"log_{0}.csv";
        public string FileTimeFormat { get; set; } = @"yyyy_MM_dd";
        public string UITimeFormat { get; set; } = @"HH:mm:ss";
        public string LogTimeFormat { get; set; } = @"yyyy/MM/dd HH:mm:ss";
        public string LogHeader { get; set; } = @"Time,Level,Log";
        private readonly Queue<LogData> qLogs;
        public int WriteThreshold = 1;
        public bool IsCounterWrite { get; private set; } = true;
        public readonly HashtableT<LogLevel, string> LevelColor;
        public readonly List<Control> ControlsBinded;
        

        private bool EnableWriteFile { get; set; }
        public bool IsEnableWriteFile
        {
            get { return EnableWriteFile; }
            set {
                EnableWriteFile = value;
                FileEvent -= AddLogFile;
                if (value)
                {
                    CheckLogProductFile();
                    FileEvent += AddLogFile;
                }
            }
        }
        private bool EnableUISynce { get; set; }
        public bool IsEnableControlSynce {
            get { return EnableUISynce; }
            set {
                EnableUISynce = value;
                UISyncEvent -= ControlSync;
                if (value)
                    UISyncEvent += ControlSync;
            }
        }
        public string LogFileName {
            get {
                return string.Format(FileNameFormat, DateTime.Now.ToString(FileTimeFormat));
            }
        }
        public string LogFilePath {
            get {
                return string.Format(@"{0}\{1}", DirectoryPath, LogFileName);
            }
        }

        public Logger(string iniFilePath = "", bool autoSyncControl = true, bool autoWirteFile = true)
        {
            qLogs = new Queue<LogData>();
            LevelColor = new HashtableT<LogLevel, string>();
            ControlsBinded = new List<Control>();

            if (!string.IsNullOrEmpty(iniFilePath))
            {
                InitIni(iniFilePath, (iniData) =>
                {
                    try
                    {
                        if (iniData == null) return;
                        DirectoryPath = iniData.SafeGet("Log File", "DirectoryPath") ?? @".\Log";
                        FileNameFormat = iniData.SafeGet("Log File", "FileNameFormat") ?? @"log_{0}.csv";
                        FileTimeFormat = iniData.SafeGet("Log File", "FileTimeFormat") ?? @"yyyy_MM_dd";
                        ///Color Data
                        foreach (LogLevel level in Enum.GetValues(typeof(LogLevel)))
                        {
                            string? color = iniData?.SafeGet("Log Level Color", level.ToString());
                            LevelColor.Add(level, color ?? "");
                        }
                        LogTimeFormat = iniData.SafeGet("Log Format", "LogTimeFormat") ?? LogTimeFormat;
                        LogHeader = iniData.SafeGet("Log Format", "Header") ?? @"Time,Level,Log";
                        UITimeFormat = iniData.SafeGet("Log Format", "UITimeFormat") ?? @"Time,Level,Log";
                    }
                    catch (Exception e)
                    {
                        SysLog.Add(LogLevel.Error, "Log設定檔讀取失敗:" + e.Message);
                    }
                });
            }
            IsEnableControlSynce = autoSyncControl;
            IsEnableWriteFile = autoWirteFile;
        }
        /// <summary> 檢查寫入目錄及檔案</summary>
        private void CheckLogProductFile()
        {
            try
            {
                if (!Directory.Exists(DirectoryPath))
                    Directory.CreateDirectory(DirectoryPath);
                if (!File.Exists(LogFilePath))
                {
                    File.Create(LogFilePath).Close();
                    using StreamWriter sw = new StreamWriter(LogFilePath, false, Encoding.UTF8);
                    sw.WriteLine(LogHeader);
                    sw.Close();
                }
            } catch(Exception) {
                throw;
            }
        }
        public void ClearLog()
        {
            lock (syncLocker)
            {
                foreach (Control c in ControlsBinded)
                {
                    c.Dispatcher.BeginInvoke(new Action(() => {
                        if (c is RichTextBox rtb)
                            rtb.Document = new FlowDocument();
                    }));
                }
            }
        }
        public void BindControl(Control control)
        {
            if (!ControlsBinded.Contains(control))
                ControlsBinded.Add(control);
        }
        readonly object syncLocker = new object();
        /// <summary> 同步呼叫 </summary>
        private void ControlSync(LogLevel level, string log)
        {
            LogData logData = new LogData(DateTime.Now, level, log);
            lock(syncLocker)
            {
                foreach (Control c in ControlsBinded)
                {
                    c.Dispatcher.BeginInvoke(new Action(() => {
                        if (c is RichTextBox rtb)
                            rtb.AppendColorLine(logData.UIString(UITimeFormat), LevelColor[level], true);
                    }));
                }
            }
        }
        private readonly object loglocker = new();
        private void AddLogFile(LogLevel level, string log)
        {
            LogData logData = new LogData(DateTime.Now, level, log);

            if (!File.Exists(LogFilePath))
                CheckLogProductFile();
           
            if (IsCounterWrite)
            {
                qLogs.Enqueue(logData);
                if(qLogs.Count >= WriteThreshold)
                {
                    lock(loglocker) {
                        WriteQueueLog();
                    }
                }
            }
            else lock (loglocker) {
                logData.WriteLog(LogFileName, LogTimeFormat);
            }
        }
        private void WriteQueueLog()
        {
            string data = "";
            if (!File.Exists(LogFilePath))
                CheckLogProductFile();
            try
            {
                while (qLogs.Count > 0)
                {
                    LogData qData = qLogs.Dequeue();
                    data += (qData.LogString(LogTimeFormat) + Environment.NewLine);
                }
                using (StreamWriter sw = new StreamWriter(LogFilePath, true, Encoding.UTF8))
                {
                    sw.WriteAsync(data);
                    sw.Close();
                }
            } catch(Exception) {
                throw;
            }
        }

        /// <summary> 程序結束時呼叫 </summary>
        public void Dispose()
        {
            LevelColor.Clear();
            ControlsBinded.Clear();
            FileEvent = null;
            UISyncEvent = null;
            if (qLogs.Count > 0)
            {
                if (EnableWriteFile && IsCounterWrite)
                    WriteQueueLog();
                else
                    qLogs.Clear();
            }
        }
    }
}
