using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PropertyChanged;
namespace Support.Data
{
    [AddINotifyPropertyChangedInterface]
    public class StringConfig
    {
        public string? FitTarget { get; set; }
        public string? Prefix { get; set; }
        public string? Suffix { get; set; }
        public int Length { get; set; }
        public bool IsAllowRedundant { get; set; } = false;

        public bool Check(string input)
        {
            if (!string.IsNullOrEmpty(FitTarget) && !input.StartsWith(FitTarget))
                return false;
            if (!string.IsNullOrEmpty(Prefix) && !input.StartsWith(Prefix))
                return false;
            if (!string.IsNullOrEmpty(Suffix) && !input.EndsWith(Suffix))
                return false;

            return true;
        }
    }
}
