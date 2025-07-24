using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
namespace Support
{
    public class TimerRoutine : IDisposable
    {
        private Timer? timer;
        public Action? Work;
        public TimeSpan TargetTime { get; set; }
        private DateTime lastTime = DateTime.MinValue;
        private bool IsCrossed = false;
        private readonly object lockObject = new();
        private readonly object workLock = new();

        public TimerRoutine()
        {
            Initialize(new TimeSpan(23,59,59), 1000);
        }
        public TimerRoutine(TimeSpan targetTime, int CheckDelay = 2000)
        {
            Initialize(targetTime,CheckDelay);
        }
        public void Initialize(TimeSpan targetTime, int CheckDelay)
        {
            Stop();
            lastTime = DateTime.Now;
            timer ??= new Timer(TimerCallback, null, 0, CheckDelay);
            TargetTime = targetTime;
        }
        private void TimerCallback(object? state)
        {
            lock (lockObject)
            {
                DateTime time_now = DateTime.Now;
                bool CrossDay = lastTime.TimeOfDay < TargetTime && time_now.TimeOfDay > TargetTime;
                if (CrossDay)
                {
                    lock (workLock) 
                    {
                        Work?.Invoke(); 
                    }
                }
                lastTime = time_now;
            }
        }

        public void Stop()
        {
            if(timer?.Change(Timeout.Infinite, Timeout.Infinite) ?? false)
            {
                timer?.Dispose();
                timer = null;
            }
        }

        public void Dispose()
        {
            timer?.Dispose();
            timer = null;
        }
    }
}
