using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Support.Data
{
    public static class IndexedExtensions
    {
        public static string GetEnumDescription(this Enum? value)
        {
            if (value == null) return "";
            FieldInfo? fieldInfo = value?.GetType()?.GetField(value.ToString());

            DescriptionAttribute[]? attributes = (DescriptionAttribute[]?)fieldInfo?.GetCustomAttributes(typeof(DescriptionAttribute), false) ?? [];

            return attributes.AsEnumerable()?.FirstOrDefault()?.Description.ToString() ?? value?.ToString() ?? "";
        }
    }
    //https://stackoverflow.com/questions/10283206/setting-getting-the-class-properties-by-string-name
    public interface IIndexedProperty
    {
        public object? this[string propertyName]
        {
            get
            {
                Type myType = typeof(IIndexedProperty);
                PropertyInfo? myPropInfo = myType?.GetProperty(propertyName);
                return myPropInfo?.GetValue(this, null);
            }
            set
            {
                Type myType = typeof(IIndexedProperty);
                PropertyInfo? myPropInfo = myType?.GetProperty(propertyName);
                myPropInfo?.SetValue(this, value, null);
            }
        }
    }
}
