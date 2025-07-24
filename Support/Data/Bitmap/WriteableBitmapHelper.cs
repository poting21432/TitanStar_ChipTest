using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace Support.Data
{
    using PixelFormat = System.Windows.Media.PixelFormat;
    public static class WriteableBitmapHelper
    {
        public static WriteableBitmap? FromByteArray(PixelFormat format, byte[] source, int width, int height )
        {
            if (source == null)
                return null;
            WriteableBitmap? wtbBmp = new WriteableBitmap(width, height, 96, 96, format, null);
            int stride = (width * format.BitsPerPixel + 7) / 8;

            wtbBmp.Lock();
            try
            {
                wtbBmp.WritePixels(new Int32Rect(0, 0, width, height), source, stride, 0);
            }
            finally
            {
                wtbBmp.Unlock();
            }
            return wtbBmp;
        }
        public static WriteableBitmap LoadFromFile(string path, PixelFormat format)
        {
            BitmapImage bitmapImage = new(new(path, UriKind.RelativeOrAbsolute));
            return new WriteableBitmap(bitmapImage);
        }
    }
}
