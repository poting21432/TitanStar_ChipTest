using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PropertyChanged;
namespace Support.Wpf.Models
{
    [AddINotifyPropertyChangedInterface]
    public class ModelSignal
    {
        public bool Value { get; set; }
        public string? Title { get; set; }
        public Action<bool>? EventSync { get; set; }

    }
}
