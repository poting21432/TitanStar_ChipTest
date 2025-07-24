using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Support.ThreadHelper
{
    public static partial class ThreadExtensions
    {
        public static bool IsProcessOpen(string processName)
        {
            Process[] processes = Process.GetProcesses();

            foreach (Process process in processes)
            {
                string Name = process.ProcessName;

                if (Name == processName)
                    return true;
            }

            return false;
        }
        public static void CheckApplicationDuplicated()
        {
            Process currentProcess = Process.GetCurrentProcess();
            var processes = Process.GetProcessesByName(currentProcess.ProcessName);
            if(processes.Length > 1)
            {
                MessageBox.Show("無法開啟程式，程式已經啟動:" + currentProcess.ProcessName, "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
            return;
        }

        /// <summary>等待 如果時間內完成 回傳true 否則回傳false, timeOut = 0 :Wait Forever</summary>
        public static bool Wait(this Func<bool> timeOutCondition,int timeOut_ms, int wait_ms = 100)
        {
            int TimeOut = Environment.TickCount + timeOut_ms;
            while (TimeOut > Environment.TickCount || timeOut_ms == 0)
            {
                if (timeOutCondition?.Invoke() ?? true)
                    return true;
                Thread.Sleep(wait_ms);
            }
            return false; //TimeOut with no condition true
        }    
    }
}
