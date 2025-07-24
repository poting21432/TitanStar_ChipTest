using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PropertyChanged;
namespace Support.Wpf.Compact
{
    [AddINotifyPropertyChangedInterface]
    public class Model_DateSelect
    {
        private DateTime? date = DateTime.Now.Date;
        public DateTime? Date
        {
            get => date;
            set
            {
                date = value;
                OnSelectDateChanged?.Invoke(value);
            }
        }
        public DateTime? DisplayDateStart { get; set; } = DateTime.MinValue;
        public DateTime? DisplayDateEnd { get; set; } = DateTime.MaxValue;
        public Action<DateTime?>? OnSelectDateChanged { get; set; }
    }
}
