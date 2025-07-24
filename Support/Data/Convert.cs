using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;

namespace Support.Data
{
    public static class Convert
    {
        public static object? ConvertToType(this string? Obj, Type? Type)
        {
            if(string.IsNullOrEmpty(Obj))
                return null;
            TypeConverter converter = TypeDescriptor.GetConverter(Type);

            if (converter.CanConvertFrom(typeof(string)))
                return converter.ConvertFrom(Obj);
            else
                throw new Exception("Type Can't Convert From String");
        }

        public static double ToDouble(this object? Obj, double DefaultValue = 0)
        {
            if (Obj == null)
                return DefaultValue;
            if (double.TryParse(Obj.ToString(), out double result))
                return result;
            else return DefaultValue;

        }

        public static int ToInt(this object? Obj, int DefaultValue = 0)
        {
            if (Obj == null)
                return DefaultValue;
            if (int.TryParse(Obj.ToString(), out int result))
                return result;
            else return DefaultValue;

        }
        public static bool? ToBool(this object Obj)
        {
            if (Obj == null)
                return null;
            if (bool.TryParse(Obj.ToString(), out bool result))
                return result;
            else return null;

        }

        public static DateTime? ToDateTime(this object Obj)
        {
            if (DateTime.TryParse(Obj.ToString(), out DateTime result))
                return result;
            else return null;
        }
    }
}
