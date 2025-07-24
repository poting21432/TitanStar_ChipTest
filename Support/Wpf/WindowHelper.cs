using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowStarter
{
    public class WindowLocationInfo
    {
        public int ScreenID { get; set; }
        public Rectangle Area => GetScreenLocation();
        public Point Location { get; set; }

        private Rectangle GetScreenLocation()
        {
            Screen s;
            if (Screen.AllScreens.Length > ScreenID)
                s = Screen.AllScreens[ScreenID];
            else
            {
                //Console.WriteLine("螢幕代號不存在");
                //Console.ReadKey(); // 等待用戶輸入任意按鍵
                return new();
            }
            return s.WorkingArea;
        }
    }
    public static class WindowHelper
    {
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);
        [DllImport("user32.dll")]
        internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        public static void MoveWindowLocation(IntPtr hwnd, WindowLocationInfo LocationInfo)
        {
            // 如果hwnd不為空，則可以修改窗口位置
            if (hwnd != IntPtr.Zero)
            {
                Rectangle area = LocationInfo.Area;
                if (area == default)
                    return;
                // 新的窗口位置（X和Y坐標）
                int newX = area.Left + LocationInfo.Location.X;
                int newY = area.Top + LocationInfo.Location.Y;
                NoTopMost(hwnd);
                // 設定新的窗口位置
                MoveWindow(hwnd, newX, newY, area.Width, area.Height, true); // SWP_NOMOVE | SWP_NOSIZE
            }
            else
            {
                Console.WriteLine("找不到目標Window");
                //Console.ReadKey(); // 等待用戶輸入任意按鍵
            }
        }
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const int HWND_TOPMOST = -1;
        private const int HWND_NOTOPMOST = -2;
        public static void NoTopMost(IntPtr hwnd)=>SetWindowPos(hwnd, (IntPtr)HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
        public static void TopMost(IntPtr hwnd) => SetWindowPos(hwnd, (IntPtr)HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
    }
}
