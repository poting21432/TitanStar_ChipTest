using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Support.Wpf
{
    public class ObservableCollectionMR<T> : ObservableCollection<T>
    {
        private bool _suppressNotification = false;

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (!_suppressNotification)
                base.OnCollectionChanged(e);
        }

        public void AddRange(IEnumerable<T> list)
        {
            ArgumentNullException.ThrowIfNull(list);

            _suppressNotification = true;

            foreach (T item in list)
                Add(item);

            _suppressNotification = false;
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
        public void RemoveRange(IEnumerable<T> list)
        {
            ArgumentNullException.ThrowIfNull(list);

            _suppressNotification = true;

            foreach (T item in list)
            {
                Remove(item);
            }

            _suppressNotification = false;
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public void RemoveRange(IEnumerable<T> list, IEqualityComparer<T> comparer)
        {
            ArgumentNullException.ThrowIfNull(list);
            ArgumentNullException.ThrowIfNull(comparer);

            _suppressNotification = true;

            foreach (T item in list)
            {
                for (int i = 0; i < Items.Count; i++)
                {
                    if (comparer.Equals(Items[i], item))
                    {
                        RemoveAt(i);
                        i--; 
                    }
                }
            }

            _suppressNotification = false;
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}
