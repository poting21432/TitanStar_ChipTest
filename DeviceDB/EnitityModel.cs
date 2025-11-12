using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Windows.Media;
using System.Data;
using System.Linq.Expressions;
using System.IO;
using System.Data.Common;
namespace DeviceDB
{
    public class DBLocal
    {
        public ConifgsDBContext ConfigsDB { get; set; } = new();

        public DataDBContext DataDB { get; set; } = new();
    }


    public partial class ConifgsDBContext : DbContext
    {
        public bool IsEnable { get; set; } = true;

        public string DBPath { get; set; } = "configs.db";

        public string DbConnectionString => string.Format("DataSource={0}", DBPath);

        public ConifgsDBContext()
        {

        }
        public void InitDataBase(string path)
        {
            DBPath = path;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (File.Exists(DBPath))
                optionsBuilder.UseSqlite(DbConnectionString);
            else
                throw new($"資料表:{DBPath}不存在");
        }
    }
    public partial class DataDBContext : DbContext
    {
        public bool IsEnable { get; set; } = true;

        public string DBPath { get; set; } = "data.db";

        public string DbConnectionString => string.Format("DataSource={0}", DBPath);

        public DataDBContext()
        {

        }
        public void InitDataBase(string path)
        {
            DBPath = path;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (File.Exists(DBPath))
                optionsBuilder.UseSqlite(DbConnectionString);
            else
                throw new($"資料表:{DBPath}不存在");
        }
    }
}

