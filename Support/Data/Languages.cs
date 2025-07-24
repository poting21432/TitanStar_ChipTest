using Support.Logger;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Support.Data
{
    public static class Languages
    {
        private static ResourceDictionary? LangResourceRef = null;
        public static void Load(Application App, string culture = "")
        {
            CultureInfo currentCultureInfo = (string.IsNullOrEmpty(culture))
                ? CultureInfo.CurrentCulture
                : CultureInfo.GetCultureInfo(culture);
            string uriPath = string.Format(@"Lang\{0}.xaml", currentCultureInfo.Name);
            if (LangResourceRef != null && App.Resources.MergedDictionaries.Contains(LangResourceRef))
                App.Resources.MergedDictionaries.Remove(LangResourceRef);

            LangResourceRef = Application.LoadComponent(new Uri(uriPath, UriKind.Relative)) as ResourceDictionary;

            if (LangResourceRef != null)
                App.Resources.MergedDictionaries.Add(LangResourceRef);
        }
    }
}
