using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace _20200117gensyoku
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            string path = "";
            (byte[] pixels, BitmapSource source) = MakeBitmapSourceAndPixelData(path, PixelFormats.Bgra32);
            List<Color> listColor = new List<Color> { Colors.White, Colors.Black, Colors.Red, Colors.Green, Colors.Blue };

        }



        private BitmapSource ReduceColor指定色で減色(BitmapSource source, List<Color> palette)
        {
            //if (OriginBitmap == null) { return source; }
            //if (palette.Count == 0) { return source; }
            var wb = new WriteableBitmap(source);
            int h = wb.PixelHeight;
            int w = wb.PixelWidth;
            int stride = wb.BackBufferStride;
            byte[] pixels = new byte[h * stride];
            wb.CopyPixels(pixels, stride, 0);
            for (int y = 0; y < h; ++y)
            {
                Parallel.For(0, w, x =>
                {
                    XParallel(y, stride, x, palette, pixels);
                });
            }
            wb.WritePixels(new Int32Rect(0, 0, w, h), pixels, stride, 0);

            return OptimisationPixelFormat(wb, palette.Count);
        }

        private void XParallel(int y, int stride, int x, List<Color> palette, byte[] pixels)
        {
            var p = y * stride + (x * 4);
            var myColor = Color.FromRgb(pixels[p + 2], pixels[p + 1], pixels[p]);
            double min, distance;
            int pIndex;

            min = GetColorDistance(myColor, palette[0]);
            pIndex = 0;
            for (int i = 0; i < palette.Count; ++i)
            {
                distance = GetColorDistance(myColor, palette[i]);
                if (min > distance)
                {
                    min = distance;
                    pIndex = i;
                }
            }
            myColor = palette[pIndex];
            pixels[p + 2] = myColor.R;
            pixels[p + 1] = myColor.G;
            pixels[p] = myColor.B;
            pixels[p + 3] = 255;//アルファ値を255に変更、完全不透明にする
        }


        //PixelFormatを色数に合わせたものに変更、これもこれは色が変化してしまうかも？
        private BitmapSource OptimisationPixelFormat(BitmapSource source, int colorCount)
        {
            PixelFormat pixelFormat;
            if (colorCount <= 2)
            {
                pixelFormat = PixelFormats.Indexed1;
            }
            else if (colorCount <= 4)
            {
                pixelFormat = PixelFormats.Indexed2;
            }
            else if (colorCount <= 16)
            {
                pixelFormat = PixelFormats.Indexed4;
            }
            else if (colorCount <= 256)
            {
                pixelFormat = PixelFormats.Indexed8;
            }
            else
            {
                pixelFormat = PixelFormats.Bgr24;
            }
            return new FormatConvertedBitmap(source, pixelFormat, null, 0);
        }

        //距離
        private double GetColorDistance(Color c1, Color c2)
        {
            return Math.Sqrt(
                Math.Pow(c1.R - c2.R, 2) +
                Math.Pow(c1.G - c2.G, 2) +
                Math.Pow(c1.B - c2.B, 2));
        }
        private double GetColorDistance(double r, double g, double b, Color c)
        {
            return Math.Sqrt(
                Math.Pow(c.R - r, 2) +
                Math.Pow(c.G - g, 2) +
                Math.Pow(c.B - b, 2));
        }

        /// <summary>
        /// 画像ファイルからbitmapと、そのbyte配列を返す、ピクセルフォーマットは指定したものに変換
        /// </summary>
        /// <param name="filePath">画像ファイルのフルパス</param>
        /// <param name="pixelFormat">PixelFormatsを指定、null指定ならBgra32で作成する</param>
        /// <param name="dpiX">96が基本、指定なしなら元画像と同じにする</param>
        /// <param name="dpiY">96が基本、指定なしなら元画像と同じにする</param>
        /// <returns></returns>
        private (byte[] pixels, BitmapSource source) MakeBitmapSourceAndPixelData(
            string filePath,
            PixelFormat pixelFormat,
            double dpiX = 0, double dpiY = 0)
        {
            byte[] pixels = null;//PixelData
            BitmapSource source = null;
            if (pixelFormat == null) { pixelFormat = PixelFormats.Bgra32; }
            try
            {
                using (System.IO.FileStream fs = new System.IO.FileStream(filePath, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                {
                    var frame = BitmapFrame.Create(fs);
                    var tempBitmap = new FormatConvertedBitmap(frame, pixelFormat, null, 0);
                    int w = tempBitmap.PixelWidth;
                    int h = tempBitmap.PixelHeight;
                    int stride = (w * pixelFormat.BitsPerPixel + 7) / 8;
                    pixels = new byte[h * stride];
                    tempBitmap.CopyPixels(pixels, stride, 0);
                    //dpi指定がなければ元の画像と同じdpiにする
                    if (dpiX == 0) { dpiX = frame.DpiX; }
                    if (dpiY == 0) { dpiY = frame.DpiY; }
                    //dpiを指定してBitmapSource作成
                    source = BitmapSource.Create(
                        w, h, dpiX, dpiY,
                        tempBitmap.Format,
                        tempBitmap.Palette, pixels, stride);
                };
            }
            catch (Exception)
            {
            }
            return (pixels, source);
        }
    }
}
