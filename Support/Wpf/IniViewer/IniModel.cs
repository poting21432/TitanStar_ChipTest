using IniParser.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PropertyChanged;
using System.Collections.ObjectModel;
using IniParser;
using System.IO;
using System.Xml.Linq;
namespace Support.Wpf
{
    [AddINotifyPropertyChangedInterface]
    public class IniModel
    {
        public string? IniPath { get; set; }
        public ObservableCollection<IniSectionModel> IniDataList { get; set; }
        public IniModel()
        {
            IniDataList = new ();
        }
        public void Initialize(string IniPath)
        {
            if (string.IsNullOrEmpty(IniPath))
                return;
            this.IniPath = IniPath;
            if(!File.Exists(IniPath))
                return;
            FileIniDataParser parser = new();
            IniData data = parser.ReadFile(IniPath);
            IniDataList.Clear();
            foreach (var sec in data.Sections)
            {
                IniSectionModel ini_sec = new(sec.SectionName);
                IniDataList.Add(ini_sec);

                foreach (var val in data[sec.SectionName])
                    ini_sec.KeyDataList.Add(new(val.KeyName, val.Value));
            }
        }
    }
    [AddINotifyPropertyChangedInterface]
    public class IniSectionModel
    {
        public string Section { get; set; }
        public ObservableCollection<KeyDataModel> KeyDataList { get; set; }
        public IniSectionModel(string section)
        {
            Section = section;
            KeyDataList = new ObservableCollection<KeyDataModel>();
        }
    }

    [AddINotifyPropertyChangedInterface]
    public class KeyDataModel
    {
        public string Key  { get; set; }
        public string Data { get; set; }

        public KeyDataModel(string key, string data)
        {
            Key = key;
            Data = data;
        }
    }
}
