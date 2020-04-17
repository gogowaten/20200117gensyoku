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


using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.IO;
using System.Globalization;


namespace _20200327_減色テストグレースケール
{
    static class Gensyoku
    {

        #region 誤差拡散を使った減色
        public static byte[] Gensyoku誤差拡散(byte[] palette, byte[] source, int width, int height, int stride)
        {
            int count = source.Length;
            byte[] pixels = new byte[count];//変換先画像用
            double[] gosaPixels = new double[count];//誤差計算用
            Array.Copy(source, gosaPixels, count);
            int p;//座標
            double gosa;//誤差(変換前 - 変換後)
            ////変換テーブル作成は全色分(0~255)作成
            //var cc = Enumerable.Range(0, 256).Select(x => (byte)x).ToArray();//?
            //Dictionary<byte, byte> table = MakeTable(cc, palette);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    p = y * stride + x;
                    //pixels[p] = table[source[p]];
                    double min = double.MaxValue;
                    double distance;
                    byte color = palette[0];
                    for (int i = 0; i < palette.Length; i++)
                    {
                        distance = Math.Abs(gosaPixels[p] - palette[i]);
                        if (distance < min)
                        {
                            min = distance;
                            color = palette[i];
                        }
                    }
                    pixels[p] = color;
                    gosa = gosaPixels[p] - color;
                    gosa /= 16.0;
                    if (x < width - 1)
                        SetLimitedGosa(gosaPixels, p + 1, gosa * 7, 0, 255);
                    //gosaPixels[p + 1] += gosa * 7;
                    if (y < height - 1)
                    {
                        p += stride;
                        SetLimitedGosa(gosaPixels, p, gosa * 5, 0, 255);
                        //gosaPixels[p] += gosa * 5;
                        if (x > 0)
                            SetLimitedGosa(gosaPixels, p - 1, gosa * 3, 0, 255);
                        //gosaPixels[p - 1] += gosa * 3;
                        if (x < width - 1)
                            SetLimitedGosa(gosaPixels, p + 1, gosa * 1, 0, 255);
                        //gosaPixels[p + 1] += gosa * 1;

                    }
                }
            }
            return pixels;
        }
        private static void SetLimitedGosa(double[] gosaPixels, int p, double gosa, byte lower, byte upper)
        {
            double result = gosaPixels[p] + gosa;
            if (result < lower)
                result = lower;
            if (result > upper)
                result = upper;
            gosaPixels[p] = result;
        }
        public static BitmapSource Gensyoku誤差拡散(BitmapSource bitmap, byte[] palette)
        {
            int w = bitmap.PixelWidth;
            int h = bitmap.PixelHeight;
            int stride = w;// (bitmap.Format.BitsPerPixel + 7) / 8;
            byte[] sourcePixels = new byte[h * stride];
            bitmap.CopyPixels(sourcePixels, stride, 0);
            byte[] pixels = Gensyoku誤差拡散(palette, sourcePixels, w, h, stride);
            return BitmapSource.Create(w, h, 96, 96, PixelFormats.Gray8, null, pixels, stride);
        }
        public static BitmapSource Gensyoku誤差拡散(BitmapSource bitmap, List<Color> palette)
        {
            byte[] vs = palette.Select(x => x.R).ToArray();
            return Gensyoku誤差拡散(bitmap, vs);
        }


        #endregion  誤差拡散を使った減色


        #region 変換テーブルで減色
        public static byte[] GensyokuUseTable(byte[] pixels, List<Color> colors)
        {
            return GensyokuUseTable(pixels, colors.Select(x => x.R).ToArray());
        }

        //変換テーブルを使った減色変換
        public static byte[] GensyokuUseTable(byte[] pixels, byte[] brightness)
        {
            byte[] replacedPixels = new byte[pixels.Length];

            //使用されている明度値の配列作成
            byte[] usedColor = MakeUsedColorList(pixels);
            //変換テーブル作成
            Dictionary<byte, byte> table = MakeTable(usedColor, brightness);

            //変換
            for (int i = 0; i < pixels.Length; i++)
            {
                replacedPixels[i] = table[pixels[i]];
            }
            return replacedPixels;
        }
        public static BitmapSource GensyokuUseTable(BitmapSource bitmap, byte[] palette)
        {
            int w = bitmap.PixelWidth;
            int h = bitmap.PixelHeight;
            int stride = w;// (bitmap.Format.BitsPerPixel + 7) / 8;
            byte[] sourcePixels = new byte[h * stride];
            bitmap.CopyPixels(sourcePixels, stride, 0);

            var vs = GensyokuUseTable(sourcePixels, palette);
            return BitmapSource.Create(w, h, 96, 96, PixelFormats.Gray8, null, vs, stride);
        }
        public static BitmapSource GensyokuUseTable(BitmapSource bitmap, List<Color> palette)
        {
            return GensyokuUseTable(bitmap, palette.Select(x => x.R).ToArray());
        }

        //変換テーブル作成、この色はこの色に変換するっていうテーブル
        private static Dictionary<byte, byte> MakeTable(byte[] usedColor, byte[] brightness)
        {
            var table = new Dictionary<byte, byte>(usedColor.Length);
            for (int i = 0; i < usedColor.Length; i++)
            {
                int nearIndex = 0;
                var distance = 255;
                for (int k = 0; k < brightness.Length; k++)
                {
                    var temp = Math.Abs(brightness[k] - usedColor[i]);
                    if (temp < distance)
                    {
                        distance = temp;
                        nearIndex = k;
                    }
                }
                table.Add(usedColor[i], brightness[nearIndex]);
            }
            return table;
        }
        //使用されている明度値の配列作成
        private static byte[] MakeUsedColorList(byte[] pixels)
        {
            //明度値をIndexに見立てて使用されていればtrue
            var temp = new bool[256];
            for (int i = 0; i < pixels.Length; i++)
            {
                temp[pixels[i]] = true;
            }
            //trueのIndexのリスト作成
            var colors = new List<byte>();
            for (int i = 0; i < temp.Length; i++)
            {
                if (temp[i]) colors.Add((byte)i);
            }
            //リストを配列に変換
            var vs = new byte[colors.Count];
            for (int i = 0; i < vs.Length; i++)
            {
                vs[i] = colors[i];
            }
            return vs;
        }
        #endregion


    }
}
