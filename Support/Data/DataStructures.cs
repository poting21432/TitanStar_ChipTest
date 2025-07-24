using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
namespace Support
{
    public static partial class ExtendMethods
    {
        public static T? SafeGet<T>(this Hashtable hash, object key, T? NullValue = default)
        {
            if (hash.ContainsKey(key))
                return (T?)hash[key];
            else
                return NullValue;
        }
        public static void SafeSet(this Hashtable hash, object key, object? value)
        {
            if (hash.ContainsKey(key))
                hash[key] = value;
            else
                hash.Add(key, value);
        }
        public static Hashtable LoadFrom(DataTable dt, string keyField, string valueField)
        {
            Hashtable htOut = new Hashtable();
            foreach (DataRow drIn in dt.Rows)
                if (!string.IsNullOrEmpty(keyField) && !string.IsNullOrEmpty(keyField))
                {
                    string? key   = drIn[keyField]?.ToString(),
                           value = drIn[valueField].ToString();
                    if(!string.IsNullOrEmpty(key))
                        htOut.Add(key, value);
                }
            return htOut;
        }
    }
    public class HashtableT<T1, T2> : Hashtable
    {
        public T2? this[T1 key]
        {
            get
            {
                if (key != null)
                    return this.SafeGet<T2>(key);
                else
                    return default;
            }
            set
            {
                if (key != null)
                    this.Set(key, value);
            }
        }
        public void Set(T1? key, T2? value)
        {
            if(key!=null)
                this.SafeSet(key, value);
        }
    }
    public class TupleList<T1, T2> : List<Tuple<T1, T2>>
    {
        public void Add(T1 item, T2 item2)
        {
            Add(new Tuple<T1, T2>(item, item2));
        }
    }
}
