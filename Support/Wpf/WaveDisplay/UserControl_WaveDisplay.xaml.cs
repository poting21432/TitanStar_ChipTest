using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Support.Wpf.WaveDisplay
{
    /// <summary>
    /// UserControl_WaveDisplay.xaml 的互動邏輯
    /// </summary>
    public partial class UserControl_WaveDisplay : UserControl
    {
        private const int MaxSamples = 200; // 最大樣本數
        private const int SampleRate = 10; // 樣本速率（毫秒）

        public int WaveCount = 3;
        private List<Polyline> waveform;
        private List<Queue<double>> samples;
        private DispatcherTimer timer;

        public UserControl_WaveDisplay()
        {
            InitializeComponent();

            waveform = new List<Polyline>();
            samples = new List<Queue<double>>(MaxSamples);

            waveform.Clear();
            samples.Clear();
            for (int i = 0; i < WaveCount; i++)
            {
                samples.Add(new Queue<double>());
                var P = new Polyline()
                {
                    Stroke = Brushes.Blue,
                    StrokeThickness = 2
                };
                waveform.Add(P);
                canvas.Children.Add(P);
            }
            foreach(var sample in samples)
                sample.Enqueue(0);
            timer = new DispatcherTimer {
                Interval = TimeSpan.FromMilliseconds(SampleRate)
            };
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            DrawWaveform();
        }
        public void AddValue(int sID,double Y)
        {
            samples[sID].Enqueue(Y);

            if (samples.Count > MaxSamples)
                samples[sID].Dequeue();
        }
        private void DrawGridLines(int numGridsX, int numGridsY)
        {
            // 清除先前的格線
            canvas.Children.OfType<Line>().Where(line => line.Tag?.ToString() == "GridLine").ToList().ForEach(line => canvas.Children.Remove(line));

            // 繪製 X 軸格線
            double xIncrement = canvas.ActualWidth / numGridsX;
            for (int i = 0; i <= numGridsX; i++)
            {
                double xPos = i * xIncrement;
                Line line = new Line
                {
                    X1 = xPos,
                    Y1 = 0,
                    X2 = xPos,
                    Y2 = canvas.ActualHeight,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1,
                    Tag = "GridLine"
                };
                canvas.Children.Add(line);
            }

            // 繪製 Y 軸格線
            double yIncrement = canvas.ActualHeight / numGridsY;
            for (int i = 0; i <= numGridsY; i++)
            {
                double yPos = i * yIncrement;
                Line line = new Line
                {
                    X1 = 0,
                    Y1 = yPos,
                    X2 = canvas.ActualWidth,
                    Y2 = yPos,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1,
                    Tag = "GridLine"
                };
                canvas.Children.Add(line);
            }
        }
        private void DrawWaveform()
        {

            double xIncrement = canvas.ActualWidth / (MaxSamples - 1);
            double x = 0;
            for(int i =0;i< WaveCount;i++)
            {
                waveform[i].Points.Clear();
                Queue<double> sample = samples[i];
                foreach (double y in sample)
                {
                    double normalizedY = canvas.ActualHeight - y;
                    waveform[i].Points.Add(new Point(x, normalizedY));
                    x += xIncrement;
                }
            }
                
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            DrawGridLines(10, 10);
        }
    }
}
