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
namespace Device
{
    public partial class MainDBContext : DbContext
    {
        public bool IsEnable { get; set; } = true;

        public string DBPath { get; set; } = "data.db";

        public string DbConnectionString => string.Format("DataSource={0}", DBPath);

        public MainDBContext()
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
            //else
            //SysLog.Add(LogLevel.Error, $"資料讀取失敗:{DBPath}");
        }
    }
}

