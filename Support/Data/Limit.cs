using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PropertyChanged;
namespace Support.Data
{
    public static class LimitExtensions
    {
        public static bool IsInRange<T>(this T value, T min, T max) where T : IComparable<T>
        {
            return value.CompareTo(min) >= 0 && value.CompareTo(max) <= 0;
        }
    }
    [AddINotifyPropertyChangedInterface]
    public class Limit
    {
        public string? Title { get; set; }
        public double LowerLimit { get; set; } = double.MinValue;
        public double UpperLimit { get; set; } = double.MaxValue;
        public Limit()
        {

        }
        public Limit(string title,double lower_limit, double upper_limit)
            =>Set(title, lower_limit, upper_limit);

        public void Set(string title, double lower_limit, double upper_limit)
        {
            Title = title;
            LowerLimit = lower_limit;
            UpperLimit = upper_limit;
        }
        public double GetRandom()
        {
             Random rand = new();
             return rand.RandomDouble(LowerLimit, UpperLimit);
        }

        public bool IsWithin(double value)
            => (value >= LowerLimit && value <= UpperLimit);
        
    }
}
