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
using System.Diagnostics;//ストップウォッチ用
using System.Numerics;//SIMDのVector用

//参照したところ
//小ネタ 並列化 | ++C++; // 未確認飛行 C ブログ
//https://ufcpp.net/blog/2016/12/tipsparallelism/


namespace _20200117gensyoku
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        BitmapSource OriginBitmapSource;
        byte[] OriginPixels;
        string ImageFileFullPath;

        public MainWindow()
        {
            InitializeComponent();

            this.AllowDrop = true;
            this.Drop += MainWindow_Drop;

            //string path = @"D:\ブログ用\チェック用2\WP_20200111_09_25_36_Pro_2020_01_11_午後わてん.jpg";
            //(OriginPixels, OriginBitmapSource) = MakeBitmapSourceAndPixelData(path, PixelFormats.Bgra32, 96, 96);
            //MyImageOrigin.Source = OriginBitmapSource;


        }

        private void MainWindow_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) == false) { return; }
            string[] filePath = (string[])e.Data.GetData(DataFormats.FileDrop);
            (OriginPixels, OriginBitmapSource) = MakeBitmapSourceAndPixelData(filePath[0], PixelFormats.Bgra32, 96, 96);

            if (OriginBitmapSource == null)
            {
                MessageBox.Show("画像として開くことができなかった");
            }
            else
            {
                MyImageOrigin.Source = OriginBitmapSource;
                MyImage.Source = OriginBitmapSource;
                ImageFileFullPath = filePath[0];
            }
        }

        //白、黒、赤、緑、青、黄色、水色、赤紫の固定8色パレット作成
        private List<byte[]> MakePalette()
        {
            List<byte[]> palette = new List<byte[]> {
                new byte[] { 255, 255, 255 },
                new byte[] { 0, 0, 0 },
                new byte[] { 255, 0, 0 },
                new byte[] { 0, 255, 0 },
                new byte[] { 0, 0, 255 },
                new byte[] { 255, 255, 0 },
                new byte[] { 0, 255, 255 },
                new byte[] { 255, 0, 255 } };
            return palette;
        }
        //ランダム色パレット作成
        private List<byte[]> MakePalette(int count)
        {
            byte[] temp = new byte[count * 3];
            // Random r = new Random((int)DateTime.Now.Ticks);
            Random r = new Random();
            r.NextBytes(temp);
            List<byte[]> palette = new List<byte[]>();
            for (int i = 0; i < temp.Length; i += 3)
            {
                palette.Add(new byte[] { temp[i], temp[i + 1], temp[i + 2] });
            }
            return palette;
        }

        //1:普通、シングルスレッド
        private BitmapSource Zs1減色1(BitmapSource source, byte[] pixels, List<byte[]> palette)
        {
            byte[] zPixels = new byte[pixels.Length];
            for (int i = 0; i < OriginPixels.Length; i += 4)
            {
                //パレットの中から、今のPixelの色に一番近い色を取得
                double min = ColorDistance(pixels[i + 2], pixels[i + 1], pixels[i], palette[0]);
                int pIndex = 0;
                for (int pc = 1; pc < palette.Count; pc++)
                {
                    var distance = ColorDistance(pixels[i + 2], pixels[i + 1], pixels[i], palette[pc]);
                    if (min > distance)
                    {
                        min = distance;
                        pIndex = pc;
                    }
                }
                //一番近い色に置き換える
                zPixels[i + 3] = 255;//A
                zPixels[i + 2] = palette[pIndex][0];//R
                zPixels[i + 1] = palette[pIndex][1];//G
                zPixels[i] = palette[pIndex][2];//B
            }
            return BitmapSource.Create(source.PixelWidth, source.PixelHeight, 96, 96, source.Format, null, zPixels, source.PixelWidth * 4);
        }

        //2:普通、シングルスレッド
        private BitmapSource Zs2減色2(BitmapSource source, byte[] pixels, List<byte[]> palette)
        {
            byte[] zPixels = new byte[pixels.Length];
            byte r, g, b;
            for (int i = 0; i < OriginPixels.Length; i += 4)
            {
                r = pixels[i + 2]; g = pixels[i + 1]; b = pixels[i];//1と違うところ
                double min = ColorDistance(r, g, b, palette[0]);
                int pIndex = 0;
                //パレットから一番近い色
                for (int pc = 1; pc < palette.Count; pc++)
                {
                    var distance = ColorDistance(r, g, b, palette[pc]);
                    if (min > distance)
                    {
                        min = distance;
                        pIndex = pc;
                    }
                }
                zPixels[i + 3] = 255;//A
                zPixels[i + 2] = palette[pIndex][0];//R
                zPixels[i + 1] = palette[pIndex][1];//G
                zPixels[i] = palette[pIndex][2];//B
            }
            return BitmapSource.Create(source.PixelWidth, source.PixelHeight, 96, 96, source.Format, null, zPixels, source.PixelWidth * 4);
        }

        //3:Parallelでの並列処理
        private BitmapSource ZmP減色3(BitmapSource source, byte[] pixels, List<byte[]> palette)
        {
            byte[] zPixels = new byte[pixels.Length];
            List<byte[]> MyList = MakeByteRGBA(pixels);//1ピクセルごとのRGBAのByte配列のリスト
            Parallel.For(0, MyList.Count, n =>
            {
                double min = ColorDistance(MyList[n], palette[0]);
                int pIndex = 0;
                for (int i = 1; i < palette.Count; i++)
                {
                    var distance = ColorDistance(MyList[n], palette[i]);
                    if (min > distance)
                    {
                        min = distance;
                        pIndex = i;
                    }
                }
                var p = n * 4;
                zPixels[p] = palette[pIndex][2];
                zPixels[p + 1] = palette[pIndex][1];
                zPixels[p + 2] = palette[pIndex][0];
                zPixels[p + 3] = 255;
            });
            return BitmapSource.Create(source.PixelWidth, source.PixelHeight, 96, 96, source.Format, null, zPixels, source.PixelWidth * 4);
        }

        //4:Taskを使った並列処理、1ピクセルごとのRGBAのByte配列のリスト
        private BitmapSource ZmT1減色4(BitmapSource source, byte[] pixels, List<byte[]> palette)
        {
            byte[] zPixels = new byte[pixels.Length];
            List<byte[]> MyList = MakeByteRGBA(pixels);//1ピクセルごとのRGBAのByte配列のリスト
            int MyThread = Environment.ProcessorCount;
            int windowSize = MyList.Count / MyThread;//1スレッドに割り当てる量

            Task.WhenAll(Enumerable.Range(0, MyThread)
                .Select(n => Task.Run(() =>
                {
                    for (int np = n * windowSize; np < (n + 1) * windowSize; np++)
                    {
                        double min = ColorDistance(MyList[np], palette[0]);
                        int pIndex = 0;
                        for (int i = 1; i < palette.Count; i++)
                        {
                            var distance = ColorDistance(MyList[np], palette[i]);
                            if (min > distance)
                            {
                                min = distance;
                                pIndex = i;
                            }
                        }
                        var p = np * 4;
                        zPixels[p] = palette[pIndex][2];
                        zPixels[p + 1] = palette[pIndex][1];
                        zPixels[p + 2] = palette[pIndex][0];
                        zPixels[p + 3] = 255;
                    }
                }))).Wait();

            return BitmapSource.Create(source.PixelWidth, source.PixelHeight, 96, 96, source.Format, null, zPixels, source.PixelWidth * 4);
        }

        //5:Taskを使った並列処理
        private BitmapSource ZmT2減色5(BitmapSource source, byte[] pixels, List<byte[]> palette)
        {
            byte[] zPixels = new byte[pixels.Length];
            int MyThread = Environment.ProcessorCount;
            int windowSize = pixels.Length / MyThread;//1スレッドに割り当てる量

            Task.WhenAll(Enumerable.Range(0, MyThread)
                .Select(n => Task.Run(() =>
                {
                    for (int p = n * windowSize; p < (n + 1) * windowSize; p += 4)
                    {
                        var r = pixels[p + 2];
                        var g = pixels[p + 1];
                        var b = pixels[p];
                        double min = ColorDistance(r, g, b, palette[0]);
                        int pIndex = 0;
                        for (int i = 1; i < palette.Count; i++)
                        {
                            var distance = ColorDistance(r, g, b, palette[i]);
                            if (min > distance)
                            {
                                min = distance;
                                pIndex = i;
                            }
                        }

                        zPixels[p] = palette[pIndex][2];
                        zPixels[p + 1] = palette[pIndex][1];
                        zPixels[p + 2] = palette[pIndex][0];
                        zPixels[p + 3] = 255;
                    }
                }))).Wait();

            return BitmapSource.Create(source.PixelWidth, source.PixelHeight, 96, 96, source.Format, null, zPixels, source.PixelWidth * 4);
        }

        //6:シングルスレッド＋SIMD
        private BitmapSource Zs1SIMD減色6(BitmapSource source, byte[] pixels, List<byte[]> palette)
        {
            byte[] zPixels = new byte[pixels.Length];
            for (int i = 0; i < OriginPixels.Length; i += 4)
            {
                double min = ColorDistanceV3(pixels[i + 2], pixels[i + 1], pixels[i], palette[0]);
                int pIndex = 0;
                //パレットから一番近い色
                for (int pc = 1; pc < palette.Count; pc++)
                {
                    //色の距離取得でSIMDのVectorを使用
                    var distance = ColorDistanceV3(pixels[i + 2], pixels[i + 1], pixels[i], palette[pc]);
                    if (min > distance)
                    {
                        min = distance;
                        pIndex = pc;
                    }
                }
                //一番近い色に置き換える
                zPixels[i + 3] = 255;//A
                zPixels[i + 2] = palette[pIndex][0];//R
                zPixels[i + 1] = palette[pIndex][1];//G
                zPixels[i] = palette[pIndex][2];//B
            }
            return BitmapSource.Create(source.PixelWidth, source.PixelHeight, 96, 96, source.Format, null, zPixels, source.PixelWidth * 4);
        }

        //7:Parallelでの並列処理＋SIMD
        private BitmapSource ZmPSIMD減色7(BitmapSource source, byte[] pixels, List<byte[]> palette)
        {
            byte[] zPixels = new byte[pixels.Length];
            List<byte[]> MyList = MakeByteRGBA(pixels);//1ピクセルごとのRGBAのByte配列のリスト
            Parallel.For(0, MyList.Count, n =>
            {
                double min = ColorDistanceV3(MyList[n], palette[0]);
                int pIndex = 0;
                for (int i = 1; i < palette.Count; i++)
                {
                    var distance = ColorDistanceV3(MyList[n], palette[i]);
                    if (min > distance)
                    {
                        min = distance;
                        pIndex = i;
                    }
                }
                var p = n * 4;
                zPixels[p] = palette[pIndex][2];
                zPixels[p + 1] = palette[pIndex][1];
                zPixels[p + 2] = palette[pIndex][0];
                zPixels[p + 3] = 255;
            });
            return BitmapSource.Create(source.PixelWidth, source.PixelHeight, 96, 96, source.Format, null, zPixels, source.PixelWidth * 4);
        }

        //8:Taskを使った並列処理+SIMD、1ピクセルごとのRGBAのByte配列のリスト
        private BitmapSource ZmT1SIMD減色8(BitmapSource source, byte[] pixels, List<byte[]> palette)
        {
            byte[] zPixels = new byte[pixels.Length];
            List<byte[]> MyList = MakeByteRGBA(pixels);//1ピクセルごとのRGBAのByte配列のリスト
            int MyThread = Environment.ProcessorCount;
            int windowSize = MyList.Count / MyThread;//1スレッドに割り当てる量

            Task.WhenAll(Enumerable.Range(0, MyThread)
                .Select(n => Task.Run(() =>
                {
                    for (int np = n * windowSize; np < (n + 1) * windowSize; np++)
                    {
                        double min = ColorDistanceV3(MyList[np], palette[0]);
                        int pIndex = 0;
                        for (int i = 1; i < palette.Count; i++)
                        {
                            var distance = ColorDistanceV3(MyList[np], palette[i]);
                            if (min > distance)
                            {
                                min = distance;
                                pIndex = i;
                            }
                        }
                        var p = np * 4;
                        zPixels[p] = palette[pIndex][2];
                        zPixels[p + 1] = palette[pIndex][1];
                        zPixels[p + 2] = palette[pIndex][0];
                        zPixels[p + 3] = 255;
                    }
                }))).Wait();

            return BitmapSource.Create(source.PixelWidth, source.PixelHeight, 96, 96, source.Format, null, zPixels, source.PixelWidth * 4);
        }

        //9:Taskを使った並列処理+SIMD
        private BitmapSource ZmT2SIMD減色9(BitmapSource source, byte[] pixels, List<byte[]> palette)
        {
            byte[] zPixels = new byte[pixels.Length];
            int MyThread = Environment.ProcessorCount;
            int windowSize = pixels.Length / MyThread;//1スレッドに割り当てる量

            Task.WhenAll(Enumerable.Range(0, MyThread)
                .Select(n => Task.Run(() =>
                {
                    for (int p = n * windowSize; p < (n + 1) * windowSize; p += 4)
                    {
                        var r = pixels[p + 2];
                        var g = pixels[p + 1];
                        var b = pixels[p];
                        double min = ColorDistanceV3(r, g, b, palette[0]);
                        int pIndex = 0;
                        for (int i = 1; i < palette.Count; i++)
                        {
                            var distance = ColorDistanceV3(r, g, b, palette[i]);
                            if (min > distance)
                            {
                                min = distance;
                                pIndex = i;
                            }
                        }

                        zPixels[p] = palette[pIndex][2];
                        zPixels[p + 1] = palette[pIndex][1];
                        zPixels[p + 2] = palette[pIndex][0];
                        zPixels[p + 3] = 255;
                    }
                }))).Wait();

            return BitmapSource.Create(source.PixelWidth, source.PixelHeight, 96, 96, source.Format, null, zPixels, source.PixelWidth * 4);
        }



        //BGRAの配列をRGBAの4つごとのByteにする、ParallelForでの処理で使う
        private List<byte[]> MakeByteRGBA(byte[] pixels)
        {
            List<byte[]> MyList = new List<byte[]>();
            for (int i = 0; i < pixels.Length; i += 4)
            {
                MyList.Add(new byte[] { pixels[i + 2], pixels[i + 1], pixels[i], pixels[i + 3] });
            }
            return MyList;
        }



        //色の距離、Mathクラスで
        private double ColorDistance(byte r1, byte g1, byte b1, byte[] palette)
        {
            return Math.Sqrt(
                Math.Pow(r1 - palette[0], 2) +
                Math.Pow(g1 - palette[1], 2) +
                Math.Pow(b1 - palette[2], 2));
        }
        private double ColorDistance(byte[] RGBA, byte[] palette)
        {
            return Math.Sqrt(
                Math.Pow(RGBA[0] - palette[0], 2) +
                Math.Pow(RGBA[1] - palette[1], 2) +
                Math.Pow(RGBA[2] - palette[2], 2));
        }
        //色の距離、SIMDを使うVectorクラスで計算
        private float ColorDistanceV3(byte r1, byte g1, byte b1, byte[] palette)
        {
            Vector3 value1 = new Vector3(r1, g1, b1);
            Vector3 value2 = new Vector3(palette[0], palette[1], palette[2]);
            return Vector3.Distance(value1, value2);
        }
        private float ColorDistanceV3(byte[] RGBA, byte[] palette)
        {
            Vector3 value1 = new Vector3(RGBA[0], RGBA[1], RGBA[2]);
            Vector3 value2 = new Vector3(palette[0], palette[1], palette[2]);
            return Vector3.Distance(value1, value2);
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

        private void MyGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Panel.SetZIndex(MyImageOrigin, 1);
        }

        private void MyGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Panel.SetZIndex(MyImageOrigin, -1);
        }
        private void ButtonSaveImage_Click(object sender, RoutedEventArgs e)
        {
            SaveImage((BitmapSource)MyImage.Source);
        }

        //処理時間計測
        private void MyTime(Func<BitmapSource, byte[], List<byte[]>, BitmapSource> func, TextBlock textBlock)
        {
            if (OriginBitmapSource == null) return;
            var sw = new Stopwatch();
            sw.Start();
            //MyImage.Source = func(OriginBitmapSource, OriginPixels, MakePalette());
            MyImage.Source = func(OriginBitmapSource, OriginPixels, MakePalette(16));
            sw.Stop();
            textBlock.Text = $"{sw.Elapsed.Seconds}秒{sw.Elapsed.Milliseconds.ToString("000")}";
        }
        private void ButtonExe1_Click(object sender, RoutedEventArgs e)
        {
            MyTime(Zs1減色1, tbTime1);
        }


        private void ButtonExe2_Click(object sender, RoutedEventArgs e)
        {
            MyTime(Zs2減色2, tbTime2);
        }

        private void ButtonExe3_Click(object sender, RoutedEventArgs e)
        {
            MyTime(ZmP減色3, tbTime3);
        }

        private void ButtonExe4_Click(object sender, RoutedEventArgs e)
        {
            MyTime(ZmT1減色4, tbTime4);
        }

        private void ButtonExe5_Click(object sender, RoutedEventArgs e)
        {
            MyTime(ZmT2減色5, tbTime5);
        }

        private void ButtonExe6_Click(object sender, RoutedEventArgs e)
        {
            MyTime(Zs1SIMD減色6, tbTime6);
        }

        private void ButtonExe7_Click(object sender, RoutedEventArgs e)
        {
            MyTime(ZmPSIMD減色7, tbTime7);
        }

        private void ButtonExe8_Click(object sender, RoutedEventArgs e)
        {
            MyTime(ZmT1SIMD減色8, tbTime8);
        }

        private void ButtonExe9_Click(object sender, RoutedEventArgs e)
        {
            MyTime(ZmT2SIMD減色9, tbTime9);
        }


        private void SaveImage(BitmapSource source)
        {
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog();
            saveFileDialog.Filter = "*.png|*.png|*.bmp|*.bmp|*.tiff|*.tiff";
            saveFileDialog.AddExtension = true;
            saveFileDialog.FileName = System.IO.Path.GetFileNameWithoutExtension(ImageFileFullPath) + "_";
            saveFileDialog.InitialDirectory = System.IO.Path.GetDirectoryName(ImageFileFullPath);
            if (saveFileDialog.ShowDialog() == true)
            {
                BitmapEncoder encoder = new BmpBitmapEncoder();
                if (saveFileDialog.FilterIndex == 1)
                {
                    encoder = new PngBitmapEncoder();
                }
                else if (saveFileDialog.FilterIndex == 2)
                {
                    encoder = new BmpBitmapEncoder();
                }
                else if (saveFileDialog.FilterIndex == 3)
                {
                    encoder = new TiffBitmapEncoder();
                }
                encoder.Frames.Add(BitmapFrame.Create(source));

                using (var fs = new System.IO.FileStream(saveFileDialog.FileName, System.IO.FileMode.Create, System.IO.FileAccess.Write))
                {
                    encoder.Save(fs);
                }
            }
        }
    }
}
