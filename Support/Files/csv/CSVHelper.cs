using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Data.OleDb;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Support.Files
{
    public static partial class ExtendMethods
    {
        internal const string TimeFormat = "yyyy/MM/dd HH:mm:ss ";
        public static void AppendToCSV(this DataRow dr, DataTable dt, string strFilePath)
        {
            StreamWriter sw = new(strFilePath, true);

            for (int i = 0; i < dt.Columns.Count; i++)
            {
                if (!Convert.IsDBNull(dr[i]))
                {
                    string value = dr[i].ToString();
                    if (value.Contains(','))
                    {
                        value = String.Format("\"{0}\"", value);
                        sw.Write(value);
                    }
                    else if (dr[i] is DateTime time)
                        sw.Write(time.ToString(TimeFormat));
                    else
                        sw.Write(dr[i].ToString());
                }
                if (i < dt.Columns.Count - 1)
                    sw.Write(",");
            }
            sw.Write(sw.NewLine);
            sw.Close();
        }

        [GeneratedRegex(@"\(([^)]*)\)")]
        private static partial Regex GroupRegex();
        public static void ParseCSVHeader(this string Header, out string HeaderType, out string HeaderName)
        {
            string? h = Header?.Trim();
            HeaderType = ""; HeaderName = "";
            if (string.IsNullOrEmpty(h)) return;
            /*
                    \(         # Escaped parenthesis, means "starts with a '(' character"
                    (          # Parentheses in a regex mean "put (capture) the stuff in between into the Groups array" 
                    [^)]       # Any character that is not a ')' character
                    *          # Zero or more occurrences of the aforementioned "non ')' char"
                    )          # Close the capturing group
                    \)         # "Ends with a ')' character"
            */
            var Match = GroupRegex().Match(h).Groups;
            if (Match.Count > 1)
            {
                HeaderType = string.Format("({0})", Match[1].Value);
                HeaderName = h.RemoveFirstSubstring(HeaderType).Trim();
            }
        }
        /// <summary>
        /// Only Write Property
        /// </summary>
        public static void ToCSV<T>(this IList<T>? Data, string FilePath, IList<string>? Header = null, string FloatFormat = "F2", string ReplaceNull="", 
            bool AutoCreateHeader = true, bool IsStringCast = true)
        => "寫入CSV".TryCatch(() => {
            if (Data == null || Data.Count ==0)
            {
                File.Create(FilePath).Close();
                return;
            }
            using StreamWriter sw = new(FilePath, false);
            PropertyInfo[] propertyInfo = typeof(T).GetProperties();

            if(Header!= null)
            {
                foreach(string h in Header)
                    sw.WriteLine(h);
            }

            //Header
            if(AutoCreateHeader)
            {
                bool first = true;
                foreach (PropertyInfo property in propertyInfo)
                {
                    if (first)
                    {
                        sw.Write(property.Name);
                        first = false;
                    }
                    else
                        sw.Write("," + property.Name);
                }
                sw.Write(sw.NewLine);
            }

            foreach (T data in Data)
            {
                foreach (PropertyInfo property in propertyInfo)
                {
                    if (property != propertyInfo[0])
                        sw.Write(",");
                    Type type = property.PropertyType;
                    object? value = property?.GetValue(data);
                    string? str_value = value switch
                    {
                        double or float => ((double?)value)?.ToString(FloatFormat),
                        _ => value?.ToString(),
                    };
                    if (string.IsNullOrEmpty(str_value))
                        sw.Write($"{ReplaceNull}");
                    else
                    {
                        if (str_value?.Contains(',') ?? false)
                        {
                            if(IsStringCast)
                                value = String.Format("\"{0}\"", value);
                            sw.Write(value);
                        }
                        else if(value is DateTime time)
                            sw.Write(time.ToString(TimeFormat));
                        else if (value is string[] str_array)
                            sw.Write(string.Join(' ', str_array));
                        else
                            sw.Write(str_value);
                    }
                }
                sw.Write(sw.NewLine);
            }
            sw.Close();
        });
        /// <summary>
        /// Only Write Property
        /// </summary>
        public static void FromCSV<T>(this IList<T>? Data, string FilePath, bool IsClear = true) where T : new()
        {
            if (Data == null || string.IsNullOrEmpty(FilePath))
                return;
            if (IsClear)
                Data?.Clear();

            using StreamReader sr = new StreamReader(FilePath, Encoding.UTF8);

            PropertyInfo[] propertyInfo = typeof(T).GetProperties();
            Dictionary<string, PropertyInfo> mapProp = new();
            foreach (PropertyInfo property in propertyInfo)
                mapProp.Add(property.Name, property);

            string? headerLine = sr.ReadLine();
            string[] headers = headerLine?.Split(',') ?? Array.Empty<string>();

            while (!sr.EndOfStream)
            {
                string? line = sr.ReadLine();
                int rID = 0;
                string data = "";
                TypeConverter? converter;

                T Tdata = new();
                for (int i = 0; i < line?.Length; i++)
                {
                    switch (line[i])
                    {
                        case '\"':
                            i++;
                            while (i < line.Length && line[i] != '\"')
                                data += line[i++];
                            Type propType = mapProp[headers[rID]].PropertyType;
                            converter = TypeDescriptor.GetConverter(propType);
                            mapProp[headers[rID++]].SetValue(Tdata, converter.ConvertFromString(data));
                            data = "";
                            break;
                        case ',':
                            propType = mapProp[headers[rID]].PropertyType;
                            converter = TypeDescriptor.GetConverter(propType);
                            mapProp[headers[rID++]].SetValue(Tdata, converter.ConvertFromString(data));
                            data = "";
                            break;
                        case '\\':
                            data += line[++i];
                            break;
                        default:
                            data += line[i];
                            break;
                    }
                }
                if (rID < headers.Length)
                {
                    Type propType = mapProp[headers[rID]].PropertyType;
                    converter = TypeDescriptor.GetConverter(propType);
                    mapProp[headers[rID++]].SetValue(Tdata, converter.ConvertFromString(data));
                }
                Data?.Add(Tdata);
            }
        }
        public static void ToCSV(this DataTable dtDataTable, string strFilePath, string PreHeader ="")
        {
            if (string.IsNullOrEmpty(strFilePath))
                return;
            StreamWriter sw = new StreamWriter(strFilePath, false, Encoding.UTF8);
            if (!string.IsNullOrEmpty(PreHeader))
                sw.WriteLine(PreHeader);
            //headers    
            for (int i = 0; i < dtDataTable.Columns.Count; i++)
            {
                sw.Write(dtDataTable.Columns[i]);
                if (i < dtDataTable.Columns.Count - 1)
                {
                    sw.Write(",");
                }
            }
            sw.Write(sw.NewLine);
            foreach (DataRow dr in dtDataTable.Rows)
            {
                for (int i = 0; i < dtDataTable.Columns.Count; i++)
                {
                    if (!Convert.IsDBNull(dr[i]))
                    {
                        string? value = dr[i].ToString();
                        if (value?.Contains(',') ?? false)
                        {
                            value = String.Format("\"{0}\"", value);
                            sw.Write(value);
                        }
                        else if (dr[i] is DateTime time)
                            sw.Write(time.ToString(TimeFormat));
                        else if (dr[i] is string[])
                            sw.Write(string.Join(' ', dr[i]));
                        else
                            sw.Write(dr[i].ToString());
                    }
                    if (i < dtDataTable.Columns.Count - 1)
                        sw.Write(",");
                }
                sw.Write(sw.NewLine);
            }
            sw.Close();
        }
        public static void FromCSV(this DataTable dt, string? strFilePath, bool isClearTable, bool isFirstRowHeader = true)
        {
            try
            {
                if (dt == null || string.IsNullOrEmpty(strFilePath)) 
                    return;
                if (!File.Exists(strFilePath))
                {
                    SysLog.Add(Logger.LogLevel.Error, "讀取Csv檔失敗:路徑不存在 " + strFilePath);
                    return;
                }
                if (isClearTable)
                    dt?.Clear();

                using StreamReader sr = new StreamReader(strFilePath);
                string? headerLine = sr.ReadLine();
                string[] headers = headerLine?.Split(',') ?? Array.Empty<string>();
                foreach (string header in headers)
                    if (!dt?.Columns.Contains(header.Trim()) ?? false)
                        dt?.Columns.Add(header.Trim());

                while (!sr.EndOfStream)
                {
                    DataRow? dr = dt?.NewRow();
                    string? line = sr.ReadLine();
                    int rID = 0;
                    string data = "";
                    if (dr == null)
                        continue;
                    for (int i = 0; i < line?.Length; i++)
                    {
                        switch (line[i])
                        {
                            case '\"':
                                i++;
                                while (i < line.Length && line[i] != '\"')
                                    data += line[i++];
                                dr[headers[rID++]] = data;
                                data = "";
                                break;
                            case ',':
                                dr[headers[rID++]] = data;
                                data = "";
                                break;
                            case '\\':
                                data += line[++i];
                                break;
                            default:
                                data += line[i];
                                break;
                        }
                    }
                    if (rID < headers.Length)
                        dr[headers[rID++]] = data;
                    dt?.Rows.Add(dr);
                }
            }
            catch(Exception)
            {
                throw;
            }
        }

        /*
        /// <summary>從csv檔設定DataTable </summary>
        public static void GetDataTableFromCsv(this DataTable dt, string path, bool isClearTable, bool isFirstRowHeader = false)
        {
            try
            {
                if (isClearTable)
                    dt?.Clear();
                string header = isFirstRowHeader ? "Yes" : "No";
                string pathOnly = Path.GetDirectoryName(path);
                string fileName = Path.GetFileName(path);
                string sql = @"SELECT * FROM [" + fileName + "]";
                using (OleDbConnection connection = new OleDbConnection(
                          @"Provider=Microsoft.Jet.OLEDB.4.0;Data Source=" + pathOnly +
                          ";Extended Properties=\"Text;HDR=" + header + "\""))
                using (OleDbCommand command = new OleDbCommand(sql, connection))
                using (OleDbDataAdapter adapter = new OleDbDataAdapter(command))
                {
                    if (dt == null) return;
                    adapter.Fill(dt);
                    return;
                }
            }
            catch (Exception)
            {
                throw;
            }
        }*/
    }
    public delegate void EventCSVFile(CsvBase csvBase, DataTable? csvTable);
    public class CsvBase
    {
        ///Header固定格式 (型別1)型別名稱1,(型別2)型別名稱2, .....
        ///eg. (key)Station,(key)Type,(addr)Error,(addr)EMS,.....
        /// <summary>定義的開頭名稱</summary>
        public List<string?> Headers;
        /// <summary>開頭類型</summary>
        public List<string> HeaderTypes;
        public EventCSVFile? OnCSVReaded;
        private readonly DataTable csvTable;
        public CsvBase(params string[] headerTypes)
        {
            HeaderTypes = new List<string>(headerTypes);
            Headers = new List<string?>();
            csvTable = new DataTable();
        }

        /// <summary> 讀檔並設定至csvTable </summary>
        public DataTable? ReadFile(string csvpath, bool isClearTable = true)
        {
            csvTable.FromCSV(csvpath, isClearTable);
            if (csvTable.Rows.Count > 0)
                Headers = csvTable?.Rows[0].ItemArray.Select(s => s?.ToString())?.ToList() ?? new List<string?>();

            OnCSVReaded?.Invoke(this, csvTable);
            return csvTable;
        }
    }
}
