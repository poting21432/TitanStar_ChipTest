using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Support
{
    public static class StringHelper
    {
        public static string RemoveFirstSubstring(this string str, string subStr)
        {
            int index = str.IndexOf(subStr);
            string str_rmv = (index < 0)
                ? str : str.Remove(index, subStr.Length);
            return str_rmv;
        }
        public static string FormatOrEmpty(this string str, string format)
        {
            if (string.IsNullOrEmpty(str))
                return "";
            return string.Format(format, str);
        }
        public static string ReplaceFirst(this string text, string search, string replace)
        {
            int pos = text.IndexOf(search);
            if (pos < 0)
            {
                return text;
            }
            return string.Concat(text.AsSpan(0, pos), replace, text.AsSpan(pos + search.Length));
        }
        public static string RemoveNewLine(this string text)
        {
            return text.Replace("\r", "").Replace("\n", "");
        }

        public static string ConcatWith(this object text, string text2, string Symbol =" ")
        {
            if(string.IsNullOrEmpty(text?.ToString()))
                return text2;
            else
                return text.ToString() + Symbol + text2;
        }
    }
}
