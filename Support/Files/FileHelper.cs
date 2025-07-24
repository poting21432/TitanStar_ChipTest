using Support.Logger;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Windows.Forms;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
namespace Support
{
    public enum DialogFileType : long
    {
        ALL = 1 << 0, 
        CSV = 1 << 1, 
        TXT = 1 << 2, 
        BMP = 1 << 3,
        PNG = 1 << 4,
        EXCEL = 1 << 5,
    }
    
    public static class FileHelper
    {
        public static bool PingHost(this string nameOrAddress)
        {
            bool pingable = false;
            using (Ping pinger = new())
            {
                try
                {
                    PingReply reply = pinger.Send(nameOrAddress);
                    pingable = reply.Status == IPStatus.Success;
                }
                catch (PingException)
                {
                    return false;
                }
            }
            return pingable;
        }
        public static IList<string> GetFilesWithExtension(string folderPath, params string[] extension)
        {
            IList<string> matchingFiles = new List<string>();

            // 檢查目錄是否存在
            if (Directory.Exists(folderPath))
            {
                // 取得目錄中所有檔案
                string[] files = Directory.GetFiles(folderPath);

                // 遍歷所有檔案
                foreach (string file in files)
                {
                    foreach(string ext in extension)
                    {
                        // 檢查檔案的副檔名是否符合特定字串
                        if (Path.GetExtension(file).Equals(ext, StringComparison.OrdinalIgnoreCase))
                            matchingFiles.Add(file); // 將符合條件的檔案加入到結果列表中
                    }
                }
            }
            return matchingFiles;
        }
        public static bool IsPathExist(this string path)
        {
            if(path.StartsWith("\\") || path.StartsWith("//"))
            {
                if (!PingHost(path[2..].Split('\\', '/')[0]))
                    return false;
            }
            if (Directory.Exists(path))
                return true;
            else if (File.Exists(path))
                return true;
            else return false;

        }

        private static HashtableT<DialogFileType, string> DialogFileFilters = new()
        {
            {DialogFileType.ALL, "所有檔案 (*.*)|*.*"  } ,
            {DialogFileType.CSV, "CSV檔 (.csv)|*.csv"  } ,
            {DialogFileType.TXT, "文字檔 (.txt)|*.txt"  } ,
            {DialogFileType.BMP, "BMP圖檔 (.bmp)|*.bmp"  },
            {DialogFileType.PNG, "PNG圖檔 (.png)|*.png"  } ,
            {DialogFileType.EXCEL, "PNG圖檔 (.xlsx)|*.xlsx"  } ,
        };

        public static FileInfo? GetLastEditFile(string DirectoryPath)
        {
            if (!DirectoryPath.IsPathExist())
                return null;
            var files = new DirectoryInfo(DirectoryPath).GetFiles().OrderByDescending(f => f.LastWriteTime);
            if (files.Any())
                return files.First();
            else return null;
        }

        private static string GetFilters(DialogFileType FileType)
        {
            string filters = "";
            foreach (DialogFileType fileType in Enum.GetValues(typeof(DialogFileType)))
            {
                if ((FileType & fileType) == fileType && DialogFileFilters.ContainsKey(fileType))
                {
                    string? filter = DialogFileFilters[fileType];
                    if (string.IsNullOrEmpty(filters))
                        filters += filter;
                    else
                        filters += "|" + filter;
                }
            }
            return filters;
        }


        public static string OpenFileDialog(this string InitialDirectory, DialogFileType FileType = DialogFileType.ALL)
        {
            string filters = GetFilters(FileType);
            OpenFileDialog ofd = new()
            {
                DefaultExt = "*.*", // Default file extension
                InitialDirectory = InitialDirectory,
                Filter = filters, // Filter files by extension
            };
            bool? result = ofd.ShowDialog();
            return (result ?? false) ? ofd.FileName : "";
        }
        public static string OpenDirectoryDialog(this string InitialDirecotry)
        {
            using var fbd = new FolderBrowserDialog()
            {
                SelectedPath = string.IsNullOrEmpty(InitialDirecotry) ? Environment.CurrentDirectory : InitialDirecotry
            };
            DialogResult result = fbd.ShowDialog();

            return (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                 ? fbd.SelectedPath : "";
        }
        public static string SaveFileDialog(this string InitialDirectory, DialogFileType FileType = DialogFileType.ALL, string FileName = "Data")
        {
            string filters = GetFilters(FileType);
            SaveFileDialog sfd = new()
            {
                DefaultExt = "*.*",
                InitialDirectory = InitialDirectory,
                FileName = FileName,
                Filter = filters,
                CheckFileExists=false,
                AddExtension=false
            };
            bool? result = sfd.ShowDialog();

            FileInfo fInfo = new(sfd.FileName);
            if (!Directory.Exists(fInfo?.Directory?.FullName ?? ""))
                return "";
            return (result ?? false) ? sfd.FileName : "";
        }
    }
}
