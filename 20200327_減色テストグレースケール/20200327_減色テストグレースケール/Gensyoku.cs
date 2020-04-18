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
    public class Gensyoku
    {

        #region 誤差拡散を使った減色
        /// <summary>
        /// Floyd_Steinberg_dithering、8bitグレースケール専用、パレットの最小値と最大値で誤差蓄積の制限して誤差拡散処理
        /// </summary>
        /// <param name="palette">輝度</param>
        /// <param name="source">画素</param>
        /// <param name="width">画像の横ピクセル数</param>
        /// <param name="height">画像の縦ピクセル数</param>
        /// <param name="stride">画像の1行分のbyte数(1ピクセルあたりのbyte * 横ピクセル数)</param>
        /// <returns></returns>
        public static byte[] Gensyoku誤差拡散2(byte[] palette, byte[] source, int width, int height, int stride)
        {
            //パレットの最小値と最大値で誤差蓄積の制限をする
            byte lower = palette.Min();
            byte upper = palette.Max();

            int count = source.Length;
            byte[] pixels = new byte[count];//変換先画像用
            double[] gosaPixels = new double[count];//誤差計算用
            Array.Copy(source, gosaPixels, count);
            int p;//座標
            double gosa;//誤差(変換前 - 変換後)

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    p = y * stride + x;
                    //パレットから一番近い色に置き換える
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
                    pixels[p] = color;//置き換え

                    //誤差拡散
                    gosa = gosaPixels[p] - color;
                    gosa /= 16.0;
                    if (x < width - 1)
                        SetLimitedGosa(gosaPixels, p + 1, gosa * 7, lower, upper);

                    if (y < height - 1)
                    {
                        p += stride;
                        SetLimitedGosa(gosaPixels, p, gosa * 5, lower, upper);

                        if (x > 0)
                            SetLimitedGosa(gosaPixels, p - 1, gosa * 3, lower, upper);

                        if (x < width - 1)
                            SetLimitedGosa(gosaPixels, p + 1, gosa * 1, lower, upper);
                    }
                }
            }
            return pixels;
        }

        public static byte[] Gensyoku誤差拡散(byte[] palette, byte[] source, int width, int height, int stride)
        {
            int count = source.Length;
            byte[] pixels = new byte[count];//変換先画像用
            double[] gosaPixels = new double[count];//誤差計算用
            Array.Copy(source, gosaPixels, count);
            int p;//座標
            double gosa;//誤差(変換前 - 変換後)

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    p = y * stride + x;
                    //パレットから一番近い色に置き換える
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
                    pixels[p] = color;//置き換え

                    //誤差拡散
                    gosa = gosaPixels[p] - color;
                    gosa /= 16.0;
                    if (x < width - 1)
                        SetLimitedGosa(gosaPixels, p + 1, gosa * 7, 0, 255);

                    if (y < height - 1)
                    {
                        p += stride;
                        SetLimitedGosa(gosaPixels, p, gosa * 5, 0, 255);

                        if (x > 0)
                            SetLimitedGosa(gosaPixels, p - 1, gosa * 3, 0, 255);

                        if (x < width - 1)
                            SetLimitedGosa(gosaPixels, p + 1, gosa * 1, 0, 255);
                    }
                }
            }
            return pixels;
        }
        //
        /// <summary>
        /// 下限上限を設定して誤差を足す、下限上限を超えたら切り捨て
        /// </summary>
        /// <param name="gosaPixels">誤差を足した値を入れる配列</param>
        /// <param name="p">配列のインデックス</param>
        /// <param name="gosa">足す誤差</param>
        /// <param name="lower">下限</param>
        /// <param name="upper">上限</param>
        private static void SetLimitedGosa(double[] gosaPixels, int p, double gosa, byte lower, byte upper)
        {
            double result = gosaPixels[p] + gosa;
            if (result < lower)
                result = lower;
            if (result > upper)
                result = upper;
            gosaPixels[p] = result;
        }

        public static byte[] Gensyoku誤差拡散Unlimited(byte[] palette, byte[] source, int width, int height, int stride)
        {
            int count = source.Length;
            byte[] pixels = new byte[count];//変換先画像用
            double[] gosaPixels = new double[count];//誤差計算用
            Array.Copy(source, gosaPixels, count);
            int p;//座標
            double gosa;//誤差(変換前 - 変換後)

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    p = y * stride + x;
                    //パレットから一番近い色に置き換える
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
                    pixels[p] = color;//置き換え

                    //誤差拡散
                    gosa = gosaPixels[p] - color;
                    gosa /= 16.0;
                    if (x < width - 1)
                        SetUnLimitedGosa(gosaPixels, p + 1, gosa * 7);

                    if (y < height - 1)
                    {
                        p += stride;
                        SetUnLimitedGosa(gosaPixels, p, gosa * 5);

                        if (x > 0)
                            SetUnLimitedGosa(gosaPixels, p - 1, gosa * 3);

                        if (x < width - 1)
                            SetUnLimitedGosa(gosaPixels, p + 1, gosa * 1);
                    }
                }
            }
            return pixels;
        }
        private static void SetUnLimitedGosa(double[] gosaPixels, int p, double gosa)
        {
            gosaPixels[p] += gosa;
        }
        public static byte[] Gensyoku誤差拡散蛇行Unlimited(byte[] palette, byte[] source, int width, int height, int stride)
        {
            int count = source.Length;
            byte[] pixels = new byte[count];//変換先画像用
            double[] gosaPixels = new double[count];//誤差計算用
            Array.Copy(source, gosaPixels, count);
            int p;//座標
            double gosa;//誤差(変換前 - 変換後)

            for (int y = 0; y < height; y++)
            {
                if (y % 2 == 0)
                {
                    for (int x = 0; x < width; x++)
                    {
                        p = y * stride + x;
                        //パレットから一番近い色に置き換える
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
                        pixels[p] = color;//置き換え

                        //誤差拡散
                        gosa = gosaPixels[p] - color;
                        gosa /= 16.0;
                        if (x < width - 1)
                            SetUnLimitedGosa(gosaPixels, p + 1, gosa * 7);

                        if (y < height - 1)
                        {
                            p += stride;
                            SetUnLimitedGosa(gosaPixels, p, gosa * 5);

                            if (x > 0)
                                SetUnLimitedGosa(gosaPixels, p - 1, gosa * 3);

                            if (x < width - 1)
                                SetUnLimitedGosa(gosaPixels, p + 1, gosa * 1);
                        }
                    }
                }
                else
                {
                    for (int x = width - 1; x >= 0; x--)
                    {
                        p = y * stride + x;
                        //パレットから一番近い色に置き換える
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
                        pixels[p] = color;//置き換え

                        //誤差拡散
                        gosa = gosaPixels[p] - color;
                        gosa /= 16.0;
                        if (x > 0)
                            SetUnLimitedGosa(gosaPixels, p - 1, gosa * 7);

                        if (y < height - 1)
                        {
                            p += stride;
                            SetUnLimitedGosa(gosaPixels, p, gosa * 5);

                            if (x > 0)
                                SetUnLimitedGosa(gosaPixels, p - 1, gosa * 1);

                            if (x < width - 1)
                                SetUnLimitedGosa(gosaPixels, p + 1, gosa * 3);
                        }
                    }
                }
            }
            return pixels;
        }

        /// <summary>
        /// Floyd_Steinberg_dithering、8bitグレースケール専用、パレットの最小値と最大値で誤差蓄積の制限して誤差拡散処理
        /// </summary>
        /// <param name="palette">輝度</param>
        /// <param name="source">画素</param>
        /// <param name="width">画像の横ピクセル数</param>
        /// <param name="height">画像の縦ピクセル数</param>
        /// <param name="stride">画像の1行分のbyte数(1ピクセルあたりのbyte * 横ピクセル数)</param>
        /// <returns></returns>
        public static byte[] Gensyoku誤差拡散蛇行limited(byte[] palette, byte[] source, int width, int height, int stride)
        {
            //パレットの最小値と最大値で誤差蓄積の制限をする
            byte lower = palette.Min();
            byte upper = palette.Max();

            int count = source.Length;
            byte[] pixels = new byte[count];//変換先画像用
            double[] gosaPixels = new double[count];//誤差計算用
            Array.Copy(source, gosaPixels, count);
            int p;//座標
            double gosa;//誤差(変換前 - 変換後)

            for (int y = 0; y < height; y++)
            {
                if (y % 2 == 0)
                {
                    for (int x = 0; x < width; x++)
                    {
                        p = y * stride + x;
                        //パレットから一番近い色に置き換える
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
                        pixels[p] = color;//置き換え

                        //誤差拡散
                        gosa = gosaPixels[p] - color;
                        gosa /= 16.0;
                        if (x < width - 1)
                            SetLimitedGosa(gosaPixels, p + 1, gosa * 7, lower, upper);

                        if (y < height - 1)
                        {
                            p += stride;
                            SetLimitedGosa(gosaPixels, p, gosa * 5, lower, upper);

                            if (x > 0)
                                SetLimitedGosa(gosaPixels, p - 1, gosa * 3, lower, upper);

                            if (x < width - 1)
                                SetLimitedGosa(gosaPixels, p + 1, gosa * 1, lower, upper);
                        }
                    }
                }
                else
                {

                    for (int x = width - 1; x >= 0; x--)
                    {
                        p = y * stride + x;
                        //パレットから一番近い色に置き換える
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
                        pixels[p] = color;//置き換え

                        //誤差拡散
                        gosa = gosaPixels[p] - color;
                        gosa /= 16.0;
                        if (x > 0)
                            SetLimitedGosa(gosaPixels, p - 1, gosa * 7, lower, upper);

                        if (y < height - 1)
                        {
                            p += stride;
                            SetLimitedGosa(gosaPixels, p, gosa * 5, lower, upper);

                            if (x > 0)
                                SetLimitedGosa(gosaPixels, p - 1, gosa * 1, lower, upper);

                            if (x < width - 1)
                                SetLimitedGosa(gosaPixels, p + 1, gosa * 3, lower, upper);
                        }
                    }
                }
            }
            return pixels;
        }

        public static BitmapSource Gensyoku誤差拡散(BitmapSource bitmap, byte[] palette)
        {
            int w = bitmap.PixelWidth;
            int h = bitmap.PixelHeight;
            int stride = w;// (bitmap.Format.BitsPerPixel + 7) / 8;
            byte[] sourcePixels = new byte[h * stride];
            bitmap.CopyPixels(sourcePixels, stride, 0);
            byte[] pixels = Gensyoku誤差拡散蛇行limited(palette, sourcePixels, w, h, stride);
            //byte[] pixels = Gensyoku誤差拡散蛇行Unlimited(palette, sourcePixels, w, h, stride);
            //byte[] pixels = Gensyoku誤差拡散Unlimited(palette, sourcePixels, w, h, stride);
            //byte[] pixels = Gensyoku誤差拡散(palette, sourcePixels, w, h, stride);
            //byte[] pixels = Gensyoku誤差拡散2(palette, sourcePixels, w, h, stride);
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
