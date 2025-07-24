using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using PropertyChanged;
using Support.Wpf.Models;

namespace Support.Wpf;
public interface IProgressService
{
    public string Message { get; set; }
    public string Status { get; set; }
    public double ProgressValue { get; set; }
    public string ProgressData { get; set; }
    public bool IsError { get; set; }

    public bool IsCancel { get; set; }
    public Dispatcher? Dispatcher => DispatherGetter?.Invoke();
    public Func<Dispatcher?>? DispatherGetter { get; set; }

    /// <summary>用來定義Progress最小區間 </summary>
    public double MinProgress { get; set; }
    /// <summary>用來定義Progress最大區間 </summary>
    public double MaxProgress { get; set; }

    public void SetProgress(string message, double min, double max)
    {
        Dispatcher?.Invoke(() =>
        {
            Message = message;
            MinProgress = min;
            MaxProgress = max;
        });
    }
    /// <summary>
    /// 回報進度用於IProgressService
    /// </summary>
    /// <param name="progresss">-1表示不修改</param>
    /// <param name="progressData">留空表示不修改</param>
    /// <param name="status">留空表示不修改</param>
    public void Report(double progresss = -1, string progressData = "", string status = "", int Delay = 0);
    public void ReportError(double progresss = -1, string progressData = "", string status = "", int Delay = 0);
    public void ResetError(double progresss = -1, string progressData = "", string status = "", int Delay = 0);

    public Action? EventRetry { get; set; }
    public Action? EventCancel { get; set; }

    public void Close();
}

[AddINotifyPropertyChangedInterface]
public class Model_TaskProgress : IProgressService
{
    private string message = "";
    public string Message
    {
        get => message;
        set
        {
            Dispatcher?.Invoke(() => message = value);
        }
    }
    private double progressValue = 0;
    public double ProgressValue
    {
        get => progressValue;
        set
        {
            Dispatcher?.Invoke(() => progressValue = value);
        }
    }
    private string progressData = "";
    public string ProgressData
    {
        get => progressData;
        set
        {
            Dispatcher?.Invoke(() => progressData = value);
        }
    }

    private string status = "";
    public string Status
    {
        get => status;
        set
        {
            Dispatcher?.Invoke(() => status = value);
        }
    }
    private bool isError = false;
    public bool IsError
    {
        get => isError;
        set
        {
            Dispatcher?.Invoke(
                () => isError = value
            );
        }
    }
    public Dispatcher? Dispatcher => DispatherGetter?.Invoke();
    public Func<Dispatcher?>? DispatherGetter { get; set; }
    public Action? EventRetry { get; set; }
    public Action? EventCancel { get; set; }
    public Action? EventClose { get; set; }

    public ICommand? CommandCancel { get; set; } = null;
    public ICommand? CommandRetry { get; set; } = null;
    public double MinProgress { get; set; } = 0;
    public double MaxProgress { get; set; } = 100;
    public double ProgressInterval => MaxProgress - MinProgress;

    public bool IsCancel { get; set; }

    public Model_TaskProgress(Func<Dispatcher?> DispGet)
    {
        DispatherGetter = DispGet;
        CommandCancel = new RelayCommand<object>((obj) => {
            Report(-1, "取消工作", "正在取消工作");
            EventCancel?.Invoke();
            IsCancel = true;
            Report(-1, "已取消工作", "取消工作");
        });
        CommandRetry = new RelayCommand<object>((obj) => EventRetry?.Invoke());
    }

    public void Report(double progresss = -1, string progressData = "", string status = "", int Delay = 0)
    {
        if (progresss >= 0)
            ProgressValue = (progresss / 100) * ProgressInterval + MinProgress;
        if (!string.IsNullOrEmpty(progressData))
            ProgressData = progressData + string.Format("({0:F2}%)", progressValue);
        if (!string.IsNullOrEmpty(status))
            Status = status;
        if (Delay > 0)
            Thread.Sleep(Delay);
    }
    public void ReportError(double progresss = -1, string progressData = "", string status = "", int Delay = 0)
    {
        Report(progresss, progressData, status, Delay);
        IsError = true;
    }
    public void ResetError(double progresss = -1, string progressData = "", string status = "", int Delay = 0)
    {
        Report(progresss, progressData, status, Delay);
        IsError = false;
    }

    public void Close()
    {
        IsCancel = true;
        EventClose?.Invoke();
    }
}