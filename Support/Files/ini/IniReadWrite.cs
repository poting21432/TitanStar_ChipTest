using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IniParser;
using IniParser.Model;
namespace Support.IniHelper
{
    public static partial class ExtendMethods
    {
        public static string? SafeGet(this IniData iniData, string section, string key)
        {
            char sep = iniData.SectionKeySeparator;
            string sectionKey = string.Format("{0}{1}{2}", section, sep, key);
            return (iniData.TryGetKey(sectionKey, out string str)) ? str : null;
        }
    }
    public delegate void IniFileEvent(IniData? iniData);
    public class IniReadWrite
    {
        private static FileIniDataParser parser = new FileIniDataParser();
        public static IniData? IniData;
        public Action? OnIniReading;
        public IniFileEvent? OnIniReaded;
        public IniFileEvent? OnIniWriting;
        public IniFileEvent? OnIniWrited;

        public void InitIni(string path, IniFileEvent ReadEvent, bool Rewrite = false)
        {
            OnIniReaded = null;
            OnIniReaded += ReadEvent;
            if (File.Exists(path))
                ReadIni(path);
            else
                File.Create(path).Close();
            if(Rewrite)
                WriteIni(path, IniData);
        }
        public void ReadIni(string path)
        {
            OnIniReading?.Invoke();
            IniData = parser.ReadFile(path);
            OnIniReaded?.Invoke(IniData);
        }
        public void WriteIni(string path, IniData? iniData)
        {
            OnIniWriting?.Invoke(iniData);
            parser.WriteFile(path, iniData);
            OnIniWrited?.Invoke(iniData);
        }
    }
}
