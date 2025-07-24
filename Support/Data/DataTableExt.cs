using Support.Logger;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
namespace Support.Data
{
    public static class DataTableExtensions
    {
        public static void SetColumns(this DataTable dt, IEnumerable<string> columns)
        {
            dt.Columns.Clear();
            foreach (string column in columns)
                dt.Columns.Add(column);
        }

        public static void SetPrimaryKey(this DataTable dt,params string[] keyColumnNames)
        {
            List<DataColumn> d_cols = new();
            foreach(string key in keyColumnNames)
            {
                if (dt.Columns.Contains(key))
                {
                    DataColumn? dcol = dt.Columns[key];
                    if (dcol != null)
                        d_cols.Add(dcol);
                }
            }
            dt.PrimaryKey = d_cols.ToArray();
        }
        public static void AddRows<T>(this DataTable dt, IList<T> Data) where T : class
        {
            for (int i = 0; i < Data.Count; i++)
            {
                T t = Data[i];
                DataRow dr = dt.NewRow();
                dr.SetFromObject(ref t);
                dt.Rows.Add(dr);
            }
        }
        public static void CopyToDataTable(this DataTable source, DataTable destination, Func<DataRow, bool>? where = null)
        {
            DataRow[] dr_data = (where == null) 
                ? source.Select() 
                : source.AsEnumerable().Where(where).ToArray();
            string[] cols = source.GetSharedColumns(destination);
            foreach(DataRow dr in dr_data)
            {
                DataRow dr_new = destination.NewRow();
                dr.CopyTo(dr_new, cols);
                destination.Rows.Add(dr_new);
            }
        }
        public static DataTable ToDataTable<T>(this IList<T> Data) where T : class
        {
            DataTable dt = new();

            Type type = typeof(T);
            foreach(var propInfo in type.GetProperties())
                dt.Columns.Add(propInfo.Name);
            for (int i=0;i< Data.Count; i++)
            {
                T data = Data[i];
                DataRow dr = dt.NewRow();
                dr.SetFromObject(ref data);
                dt.Rows.Add(dr);
            }
            return dt;
        }
        public static void SetToList<T>(this DataTable dt, IList<T> Collection, bool Clear = true) where T : class , new()
        {
            if (Clear)
                Collection.Clear();
            foreach(DataRow dr in dt.Rows)
            {
                T data = new();
                dr.SetToObject(ref data);
                Collection.Add(data);
            }
        }
        public static void SetFromObject<T>(this DataRow? dr, ref T obj)
        {
            if (dr?.Table == null || obj == null)
                return;

            foreach (DataColumn col in dr.Table.Columns)
            {
                string? propertyName = col.ColumnName;
                if (string.IsNullOrEmpty(propertyName))
                    continue;
                PropertyInfo? property = obj?.GetType()?.GetProperty(propertyName);

                if (property != null && obj != null)
                    dr[propertyName] = property.GetValue(obj);
            }
        }
        public static void SetToObject<T>(this DataRow? dr,ref T obj)
        {
            if (dr?.Table == null || obj == null)
                return;
            try
            {
                Type type = obj.GetType();
                foreach (DataColumn col in dr.Table.Columns)
                {
                    string? propertyName = col.ColumnName;
                    if (string.IsNullOrEmpty(propertyName))
                        continue;
                    PropertyInfo? property = type.GetProperty(propertyName);
                    if (property != null && property.CanWrite && dr[propertyName] != DBNull.Value && !string.IsNullOrEmpty(dr[propertyName]?.ToString()))
                    {
                        TypeConverter converter = TypeDescriptor.GetConverter(property.PropertyType);
                        var convertedValue = converter.ConvertFrom(dr[propertyName]);
                        property.SetValue(obj, convertedValue);
                    }
                }
            }catch(Exception d)
            {
                SysLog.Add(LogLevel.Error,"轉換錯誤:" + d.Message);
            }
        }
        public static DataRow? GetFirstEmpty(this DataTable dt, string Column)
        {
            var result = from r in dt.AsEnumerable()
                         where (r.Field<string>(Column) ?? string.Empty) == string.Empty
                         select r;

            return (result.Any()) ? result.ElementAt(0) : null;
        }

        public static DataRow? SelectRow(this DataTable dt, string Column, string Value, bool IsTrim = true)
        {
            if(IsTrim)
            {
                var result = from r in dt.AsEnumerable()
                             where (r.Field<string>(Column) ?? string.Empty).Trim() == Value.Trim()
                             select r;
                return (result.Any()) ? result.ElementAt(0) : null;
            }
            else
            {
                var result = from r in dt.AsEnumerable()
                             where (r.Field<string>(Column) ?? string.Empty) == Value
                             select r;
                return (result.Any()) ? result.ElementAt(0) : null;
            }
        }
        public static void CopyTo(this DataRow row, DataRow? destination, string[]? columns = null)
        {
            if (destination == null)
                return;
            if(columns == null || row.Table == null || destination.Table == null)
                for (int i = 0; i < row.ItemArray.Length && i <destination.ItemArray.Length; i++)
                    destination[i] = row.ItemArray[i];
            else
                foreach(string col in columns)
                    destination[col] = row[col];
        }
        public static string[] GetSharedColumns(this DataTable dt1, DataTable dt2)
        {
            List<string> columns = new();
            foreach (DataColumn column in dt1.Columns)
                if (dt2.Columns.Contains(column.ColumnName))
                    columns.Add(column.ColumnName);
            return columns.ToArray();
        }
    }
}
