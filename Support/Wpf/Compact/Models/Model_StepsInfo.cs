using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Threading;

namespace Support.Wpf
{
    [AddINotifyPropertyChangedInterface]
    public class StepData
    {
        public static Brush StepColor => Brushes.Yellow;
        public static Brush OrigColor => Brushes.LightBlue;
        public Status Status { get; set; } = Status.Stop;
        public int ID { get; set; } = 0;
        public string Title { get; set; } = "";

        public bool IsShowTime = true;
        public DateTime StartTime = DateTime.MinValue;
        public DateTime EndTime = DateTime.MinValue;
        public bool IsFinished = true;
        public double TimeCost { get; set; } = 0;
        public string TimeData => (!IsFinished)
                   ? (DateTime.Now - StartTime).TotalSeconds.ToString("F1") + "s"
                   : (EndTime - StartTime).TotalSeconds.ToString("F1") + "s";

        public string TimeTag { get; set; } = "";
        public Brush ColorBrush { get; set; } = OrigColor;
        private readonly DispatcherTimer _timer = new();
        public StepData()
        {
            _timer = new();
            _timer.Interval = TimeSpan.FromSeconds(0.1);
            _timer.Tick += Timer_Tick!;
        }
        public void SetStart()
        {
            IsFinished = false;
            StartTime = DateTime.Now;
            TimeTag = "0";
            ColorBrush = StepColor;
            Status = Status.Processing;
            _timer.Start();
        }
        public void SetEnd()
        {
            EndTime = DateTime.Now;
            TimeTag = TimeData;
            IsFinished = true;
            ColorBrush = OrigColor;
            TimeCost = (EndTime - StartTime).TotalSeconds;
            _timer.Stop();
            Status = Status.Completed;
        }
        private void Timer_Tick(object sender, EventArgs e)
        {
            TimeTag = TimeData;
        }
    }
    [AddINotifyPropertyChangedInterface]
    public class Model_StepsInfo
    {
        public string Title { get; set; } = "";
        public ObservableCollection<StepData> StepsData { get; set; } = new();
    }
}
