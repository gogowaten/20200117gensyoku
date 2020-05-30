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
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //byte[] MyOriginPixels;
        BitmapSource MyOriginBitmap;//元の画像用

        List<Palette> MyPalettes = new List<Palette>();
        List<StackPanel> MyPalettePanels = new List<StackPanel>();//パレットを格納しているスタックパネル

        string ImageFileFullPath;//開いた画像ファイルのパス

        public MainWindow()
        {
            InitializeComponent();
            var neko = System.Diagnostics.FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location);
            this.Title = neko.ProductName + " ver." + neko.FileVersion;


            MyInitialize();

        }

        private void MyInitialize()
        {
            this.AllowDrop = true;
            this.Drop += MainWindow_Drop;

            ButtonGetClipboardImage.Click += ButtonGetClipboardImage_Click;
            ButtonListClear.Click += ButtonListClear_Click;
            MyImage.Source = MyOriginBitmap;
            ButtonImageStretch.Click += ButtonImageStretch_Click;
            MyImageGrid.MouseLeftButtonDown += (s, e) => Panel.SetZIndex(MyImageOrigin, 1);//前面へ
            MyImageGrid.MouseLeftButtonUp += (s, e) => Panel.SetZIndex(MyImageOrigin, -1);//背面へ


            //コンボボックスの初期化
            ComboBoxSelectType.ItemsSource = Enum.GetValues(typeof(SelectType));
            ComboBoxSelectType.SelectedIndex = 0;
            ComboBoxSplitType.ItemsSource = Enum.GetValues(typeof(SplitType));
            ComboBoxSplitType.SelectedIndex = 0;
            ComboBoxColorSelectType.ItemsSource = Enum.GetValues(typeof(ColorSelectType));
            ComboBoxColorSelectType.SelectedIndex = 0;

            //string file;
            //file = @"D:\ブログ用\テスト用画像\NEC_1456_2018_03_17_午後わてん.jpg";
            //(MyOriginPixels, MyOriginBitmap) = MakeBitmapSourceAndPixelData(file, PixelFormats.Gray8, 96, 96);
            //MyImage.Source = MyOriginBitmap;

        }



        //画像の表示方式切り替え、実寸or全体表示
        private void ButtonImageStretch_Click(object sender, RoutedEventArgs e)
        {
            if (MyImage.Stretch == Stretch.None)
            {
                MyImageOrigin.Stretch = Stretch.Uniform;
                MyImage.Stretch = Stretch.Uniform;
                MyScrollViewerImage.Content = null;
                //MyImageDockPanel.Children.Add(MyImage);
                MyImageDockPanel.Children.Add(MyImageGrid);
            }
            else
            {
                MyImageOrigin.Stretch = Stretch.None;
                MyImage.Stretch = Stretch.None;
                //MyImageDockPanel.Children.Remove(MyImage);
                MyImageDockPanel.Children.Remove(MyImageGrid);
                //MyScrollViewerImage.Content = MyImage;
                MyScrollViewerImage.Content = MyImageGrid;
            }
        }


        //すべてのリストボックス消去
        private void ButtonListClear_Click(object sender, RoutedEventArgs e)
        {
            //ClearPalettes();
            ClearPalettesWithoutIsChecked();
        }
        private void ClearPalettes()
        {
            MyPalettes.Clear();
            MyStackPanel.Children.Clear();
            MyPalettePanels.Clear();
            MyImage.Source = MyOriginBitmap;
            TextBlockTime.Text = "";
        }
        //チェックのないパレットは削除
        private void ClearPalettesWithoutIsChecked()
        {
            //チェックのないパレットとそれを格納しているスタックパネル列挙
            var palettes = MyPalettes.Where(x => x.CheckBoxIsKeepPalette.IsChecked == false).ToList();
            var panels = MyPalettePanels.Where(x => ((Palette)x.Tag).CheckBoxIsKeepPalette.IsChecked == false).ToList();
            //削除
            foreach (var item in palettes)
            {
                MyPalettes.Remove(item);
            }
            foreach (var item in panels)
            {
                MyStackPanel.Children.Remove(item);
            }
            MyImage.Source = MyOriginBitmap;
            TextBlockTime.Text = "";
        }

        //パレット作成ボタン押した時
        private void ButtonMakePalette_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            int colorCount = int.Parse(button.Content.ToString());
            if (RadioButtonPaletteColorSelect.IsChecked == true)
                MakePaletteAllColorSelectType(colorCount);//色選択方法すべてのパレット
            else if (RadioButtonPaletteSelect.IsChecked == true)
                MakePaletteAllSelectType(colorCount);
            else if (RadioButtonPaletteSplit.IsChecked == true)
                MakePaletteAllSplitType(colorCount);
            else
                MakePaletteColor(colorCount);//フリー
        }

        //減色パレット作成
        private void MakePaletteColor(int colorCount)
        {
            if (MyOriginBitmap == null) return;

            var sw = new Stopwatch();
            sw.Start();

            //指定色数に分割したCube作成
            Cube cube = MakeSplittedCube(colorCount);

            //Cubeから色取得して、色データ作成
            List<Color> colors = cube.GetColors((ColorSelectType)ComboBoxColorSelectType.SelectedItem);

            //パレット作成して追加表示
            MakePaletteStackPanel(colors);

            sw.Stop();
            //TextBlockTime.Text = $"{sw.Elapsed.TotalSeconds.ToString("F3")}";
            //↑は↓と同じ、F3は小数点以下3桁まで表示の意味
            //TextBlockTime.Text = $"{sw.Elapsed.TotalSeconds:F3}";
            TextBlockTime.Text = $"{sw.Elapsed.TotalSeconds:F3}(パレット作成処理時間)";

            ////色データ表示用のlistboxを作成して表示
            //ListBox list = CreateListBox();
            //list.DataContext = data;
            ////MyStackPanel.Children.Add(list);
            //MyStackPanel.Children.Add(MakePanelPalette(list, colors.Count));
        }
        //指定色数に分割したCube作成
        private Cube MakeSplittedCube(int colorCount)
        {
            if (MyOriginBitmap == null) return null;
            SelectType selecter = (SelectType)ComboBoxSelectType.SelectedItem;
            SplitType splitter = (SplitType)ComboBoxSplitType.SelectedItem;
            var cube = new Cube(MyOriginBitmap);
            cube.Split(colorCount, selecter, splitter);//分割数指定でCube分割
            return cube;
        }

        //パレット一覧作成、Cubeからの色取得方法すべてのパレット
        private void MakePaletteAllColorSelectType(int colorCount)
        {
            if (MyOriginBitmap == null) return;
            var sw = new Stopwatch();
            sw.Start();
            //指定色数に分割したCube作成
            Cube cube = MakeSplittedCube(colorCount);

            foreach (var item in Enum.GetValues(typeof(ColorSelectType)))
            {
                //Cubeから色取得して、色データ作成
                //パレット作成して追加表示
                MakePaletteStackPanel(cube.GetColors((ColorSelectType)item));
            }
            sw.Stop();
            TextBlockTime.Text = $"{sw.Elapsed.TotalSeconds:F3}({Enum.GetValues(typeof(ColorSelectType)).Length}個のパレット作成処理時間)";
        }

        //パレット一覧作成、Cube選択方法すべてのパレット
        private void MakePaletteAllSelectType(int colorCount)
        {
            if (MyOriginBitmap == null) return;
            var sw = new Stopwatch();
            sw.Start();
            var cube = new Cube(MyOriginBitmap);
            var splitter = (SplitType)ComboBoxSplitType.SelectedItem;
            var colorType = (ColorSelectType)ComboBoxColorSelectType.SelectedItem;
            foreach (var type in Enum.GetValues(typeof(SelectType)))
            {
                cube.Split(colorCount, (SelectType)type, splitter);
                MakePaletteStackPanel(cube.GetColors(colorType));
            }
            sw.Stop();
            TextBlockTime.Text = $"{sw.Elapsed.TotalSeconds:F3}({Enum.GetValues(typeof(SelectType)).Length}個のパレット作成処理時間)";
        }

        //パレット一覧作成、Cube分割方法すべてのパレット
        private void MakePaletteAllSplitType(int colorCount)
        {
            if (MyOriginBitmap == null) return;
            var sw = new Stopwatch();
            sw.Start();
            var cube = new Cube(MyOriginBitmap);
            var selecter = (SelectType)ComboBoxSelectType.SelectedItem;
            var colorType = (ColorSelectType)ComboBoxColorSelectType.SelectedItem;
            foreach (var type in Enum.GetValues(typeof(SplitType)))
            {
                cube.Split(colorCount, selecter, (SplitType)type);
                MakePaletteStackPanel(cube.GetColors(colorType));
            }
            sw.Stop();
            TextBlockTime.Text = $"{sw.Elapsed.TotalSeconds:F3}({Enum.GetValues(typeof(SplitType)).Length}個のパレット作成処理時間)";
        }



        //List<Color>からパレット作成して減色ボタン付きstackPanel作成してstackPanelに追加
        private void MakePaletteStackPanel(List<Color> colors)
        {
            var palette = new Palette(colors);
            MyPalettes.Add(palette);

            var panel = new StackPanel() { Orientation = Orientation.Horizontal };
            panel.Tag = palette;
            MyPalettePanels.Add(panel);


            var button = new Button() { Content = "減色" };//減色ボタン
            button.Click += ButtonGensyoku_Click;
            button.Tag = palette;//タグに対になるパレットを入れる
            panel.Children.Add(button);

            panel.Children.Add(palette);
            MyStackPanel.Children.Add(panel);//表示
            //表示更新
            this.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
            //MyDictionary.Add(button, palette);
        }

        //減色処理実行
        private void ButtonGensyoku_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            Palette palette = (Palette)button.Tag;
            GensyokuExe(palette);
        }
        private void GensyokuExe(Palette palette)
        {
            var sw = new Stopwatch();
            sw.Start();
            if (CheckBoxErrorDiffusion.IsChecked == true)
            {
                //誤差拡散
                MyImage.Source = Gensyoku.Gensyoku誤差拡散(MyOriginBitmap, palette.Colors);
            }
            else
            {
                //変換テーブル使用
                MyImage.Source = Gensyoku.GensyokuUseTable(MyOriginBitmap, palette.Colors);
            }
            sw.Stop();
            TextBlockTime.Text = $"{sw.Elapsed.TotalSeconds:F3}(減色変換処理時間)";

            //int stride = MyOriginBitmap.PixelWidth;// * MyOriginBitmap.Format.BitsPerPixel / 8;
            //MyImage.Source = BitmapSource.Create(MyOriginBitmap.PixelWidth, MyOriginBitmap.PixelHeight, 96, 96, MyOriginBitmap.Format, null, vs, stride);
        }



        //クリップボードの画像取得、グレースケール化
        private void ButtonGetClipboardImage_Click(object sender, RoutedEventArgs e)
        {
            if (!Clipboard.ContainsImage()) return;

            MyOriginBitmap = Clipboard.GetImage();
            MyOriginBitmap = new FormatConvertedBitmap(MyOriginBitmap, PixelFormats.Gray8, null, 0);
            MyImageOrigin.Source = MyOriginBitmap;
            MyImage.Source = MyOriginBitmap;
            //int w = MyOriginBitmap.PixelWidth;
            //int h = MyOriginBitmap.PixelHeight;
            //int stride = w * 1;
            //byte[] pixels = new byte[h * stride];
            //MyOriginBitmap.CopyPixels(pixels, stride, 0);
            //MyOriginPixels = pixels;
            ClearPalettes();//パレットリスト初期化
        }

        //ファイルドロップされたとき
        private void MainWindow_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) == false) return;
            string[] filePath = (string[])e.Data.GetData(DataFormats.FileDrop);
            (byte[] pixels, BitmapSource source) = MakeBitmapSourceAndPixelData(filePath[0], PixelFormats.Gray8, 96, 96);
            if (source == null)
            {
                MessageBox.Show("ドロップされたファイルは画像として開くことができなかった");
            }
            else
            {
                MyOriginBitmap = source;
                //MyOriginPixels = pixels;
                MyImageOrigin.Source = source;
                MyImage.Source = source;
                ClearPalettes();//パレットリスト初期化
                ImageFileFullPath = System.IO.Path.GetFullPath(filePath[0]);//ファイルのフルパス取得
            }
        }

        #region 画像読み込み
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
                    //パレットリスト初期化
                    ClearPalettes();
                };
            }
            catch (Exception)
            {
            }
            return (pixels, source);
        }

        #endregion

        private void ButtonTest_Click(object sender, RoutedEventArgs e)
        {
            MakePaletteStackPanel(new List<Color> { Color.FromRgb(0, 0, 0), Color.FromRgb(85, 85, 85), Color.FromRgb(170, 170, 170), Color.FromRgb(255, 255, 255) });
            MakePaletteStackPanel(new List<Color> { Color.FromRgb(0, 0, 0), Color.FromRgb(255, 255, 255) });
            //var neko = MyStackPanel.Children;


        }

        //画像を保存
        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            if (MyOriginBitmap == null) return;
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog();
            //saveFileDialog.Filter = "*.png|*.png|*.jpg|*.jpg;*.jpeg|*.bmp|*.bmp|*.gif|*.gif|*.tiff|*.tiff|*.wdp|*.wdp;*jxr";
            saveFileDialog.Filter = "*.png|*.png|*.jpg|*.jpg;*.jpeg|*.bmp|*.bmp|*.gif|*.gif";
            saveFileDialog.AddExtension = true;//ファイル名に拡張子追加

            //初期フォルダ指定、開いている画像と同じフォルダ
            saveFileDialog.InitialDirectory = System.IO.Path.GetDirectoryName(ImageFileFullPath);
            saveFileDialog.FileName = System.IO.Path.GetFileNameWithoutExtension(ImageFileFullPath) + "_";
            if (saveFileDialog.ShowDialog() == true)
            {
                BitmapEncoder encoder = null;
                switch (saveFileDialog.FilterIndex)
                {
                    case 1:
                        encoder = new PngBitmapEncoder();
                        break;
                    case 2:
                        encoder = new JpegBitmapEncoder();
                        break;
                    case 3:
                        encoder = new BmpBitmapEncoder();
                        break;
                    case 4:
                        encoder = new GifBitmapEncoder();
                        break;
                    //case 5:
                    //    //tiffは圧縮方式をコンボボックスから取得
                    //    var tiff = new TiffBitmapEncoder();
                    //    tiff.Compression = (TiffCompressOption)ComboboxTiffCompress.SelectedItem;
                    //    encoder = tiff;
                    //    break;
                    //case 6:
                    //    //wmpはロスレス指定、じゃないと1bppで保存時に画像が崩れるしファイルサイズも大きくなる
                    //    var wmp = new WmpBitmapEncoder();
                    //    wmp.ImageQualityLevel = 1.0f;
                    //    encoder = wmp;
                    //    break;
                    default:
                        break;
                }

                encoder.Frames.Add(BitmapFrame.Create((BitmapSource)MyImage.Source));
                using (var fs = new FileStream(saveFileDialog.FileName, FileMode.Create, FileAccess.Write))
                {
                    encoder.Save(fs);
                }

            }
        }

        //パレットソート
        private void ButtonColorSort_Click(object sender, RoutedEventArgs e)
        {
            if (MyPalettes.Count == 0) return;
            foreach (var item in MyPalettes)
            {
                //昇順ソート
                item.SortColor(System.ComponentModel.ListSortDirection.Ascending);
                //降順ソート
                //item.SortColor(System.ComponentModel.ListSortDirection.Descending);
            }


        }

        //クリップボードにコピー
        private void ButtonSetClipboardImage_Click(object sender, RoutedEventArgs e)
        {
            if (MyImage.Source == null) return;
            Clipboard.SetImage((BitmapSource)MyImage.Source);
        }


        ////保存時の初期ファイル名取得
        //private string GetSaveFileName()
        //{
        //    string fileName = "";
        //    fileName = System.IO.Path.GetFileNameWithoutExtension(ImageFileFullPath);
        //    if (Radio1bpp.IsChecked == true) { fileName += "_1bpp白黒2値"; }
        //    else if (Radio8bpp.IsChecked == true) { fileName += "_8bpp白黒2値"; }
        //    else { fileName += "_32bpp白黒2値"; }
        //    return fileName;
        //}












    }


    public class Palette : StackPanel
    {
        public List<Color> Colors { get; private set; }

        //パレットクリア時にチェックが有れば残す用の目印
        public CheckBox CheckBoxIsKeepPalette { get; private set; }

        ObservableCollection<MyData> Datas { get; set; }
        public Palette(List<Color> colors)
        {

            Colors = colors;
            //DataContext作成
            Datas = MakeDataContext(colors);
            //ListBox作成
            ListBox list = CreateListBox();
            this.DataContext = Datas;
            //Panel作成
            MakePanelPalette(list, colors.Count);

        }


        //        C#のWPFでCollectionViewを使ってリスト表示をソート - Ararami Studio
        //https://araramistudio.jimdo.com/2016/10/27/wpf%E3%81%AEcollectionview%E3%82%92%E4%BD%BF%E3%81%A3%E3%81%A6%E3%83%AA%E3%82%B9%E3%83%88%E8%A1%A8%E7%A4%BA%E3%82%92%E3%82%BD%E3%83%BC%E3%83%88/

        //パレット色ソート
        public void SortColor(System.ComponentModel.ListSortDirection listSortDirection)
        {
            var cv = CollectionViewSource.GetDefaultView(this.Datas);
            cv.SortDescriptions.Clear();
            cv.SortDescriptions.Add(new System.ComponentModel.SortDescription(nameof(MyData.GrayScaleValue), listSortDirection));
        }

       



        #region 表示、初期化
        private void MakePanelPalette(ListBox listBox, int colorCount)
        {
            this.Orientation = Orientation.Horizontal;//横積み

            //キープするパレット
            CheckBoxIsKeepPalette = new CheckBox() { VerticalAlignment = VerticalAlignment.Center };
            this.Children.Add(CheckBoxIsKeepPalette);

            //色数表示用
            var tb = new TextBlock() { Text = $"{colorCount}色 ", VerticalAlignment = VerticalAlignment.Center };
            this.Children.Add(tb);

            this.Children.Add(listBox);

        }



        //Colorのリストから色データ作成
        private ObservableCollection<MyData> MakeDataContext(List<Color> colors)
        {
            var data = new ObservableCollection<MyData>();
            for (int i = 0; i < colors.Count; i++)
            {
                data.Add(new MyData(colors[i]));
            }
            return data;
        }

        //色データ表示用のlistbox作成
        //        2020WPF/MainWindow.xaml.cs at master · gogowaten/2020WPF
        //https://github.com/gogowaten/2020WPF/blob/master/20200317_ListBox/20200317_ListBox/MainWindow.xaml.cs
        private ListBox CreateListBox()
        {
            var listBox = new ListBox();
            listBox.SetBinding(ListBox.ItemsSourceProperty, new Binding());
            //listboxの要素追加方向を横にする
            var stackPanel = new FrameworkElementFactory(typeof(StackPanel));
            stackPanel.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            var itemsPanel = new ItemsPanelTemplate() { VisualTree = stackPanel };
            listBox.ItemsPanel = itemsPanel;

            //ListBoxのアイテムテンプレート作成、設定
            //ItemTemplate作成、Bindingも設定する
            //縦積みのstackPanelにBorderとTextBlock
            //StackPanel(縦積み)
            //┣Border 色表示用
            //┗TextBlock 値表示用
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.WidthProperty, 20.0);
            border.SetValue(Border.HeightProperty, 10.0);
            border.SetBinding(Border.BackgroundProperty, new Binding(nameof(MyData.Brush)));

            var textBlock = new FrameworkElementFactory(typeof(TextBlock));
            textBlock.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Right);
            textBlock.SetBinding(TextBlock.TextProperty, new Binding(nameof(MyData.GrayScaleValue)));

            var panel = new FrameworkElementFactory(typeof(StackPanel));
            //panel.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);//横積み
            panel.AppendChild(border);
            panel.AppendChild(textBlock);

            var dt = new DataTemplate();
            dt.VisualTree = panel;
            listBox.ItemTemplate = dt;
            return listBox;
        }
        #endregion 表示、初期化

    }


    //グレースケール用だから線だけどCube(立方体)
    public class Cube
    {
        public List<Cube> Cubes = new List<Cube>();//分割したCubeを入れる用
        public byte[] Pixels;
        public byte Min;
        public byte Max;
        public bool IsCalcMinMax = false;
        public byte[] SortedPixels;//ソート用
        public bool IsCalcPixelsSorted = false;
        public double Variance;//分散
        public bool IsCalcVariance = false;//分散を計算済みフラグ用
        public int[] Histogram;//大津の2値化や、Kittlerの方法用ヒストグラム
        public bool IsHistogram = false;//ヒストグラムを作成したフラグ用


        public Cube(BitmapSource bitmapSource)
        {
            int w = bitmapSource.PixelWidth;
            int h = bitmapSource.PixelHeight;
            int stride = w;
            byte[] pixels = new byte[h * stride];
            bitmapSource.CopyPixels(pixels, stride, 0);
            Pixels = pixels;
        }
        public Cube(byte[] piexels)
        {
            this.Pixels = piexels;
        }

        /// <summary>
        /// Cubeを分割してCubeのリスト作成
        /// </summary>
        /// <param name="count">分割数</param>
        /// <param name="select">分割するCubeを選択する方法</param>
        /// <param name="split">Cubeを分割する方法</param>
        public void Split(int count, SelectType select, SplitType split)
        {
            Cubes.Clear();
            Cubes.Add(this);
            var confirmCubes = new List<Cube>();//これ以上分割できないCube隔離用
            while (Cubes.Count + confirmCubes.Count < count)
            {
                Cube cube = SelectCube(Cubes, select);//選択
                (Cube aCube, Cube bCube) = SplitCube(cube, split);//2分割

                if (aCube.Pixels.Length == 0 || bCube.Pixels.Length == 0)
                {
                    //分割できなかったCubeを隔離用リストに移動
                    confirmCubes.Add(cube);
                    Cubes.Remove(cube);
                    //分割できるCubeが尽きたらループ抜け
                    if (Cubes.Count == 0) break;
                }
                else
                {
                    //分割できたCubeをリストから削除して、分割したCubeを追加
                    Cubes.Remove(cube);
                    Cubes.Add(aCube);
                    Cubes.Add(bCube);
                }
            }
            //隔離しておいたCubeを戻す
            foreach (var item in confirmCubes)
            {
                Cubes.Add(item);
            }
        }

        #region 分割するCube選択
        private Cube SelectCube(List<Cube> cubes, SelectType select)
        {
            Cube result = cubes[0];
            //辺最長(MinとMaxの差)のCube
            if (select == SelectType.SideLong)
            {
                int length = 0;
                foreach (var item in cubes)
                {
                    //MinMaxが未計算なら計算する
                    if (item.IsCalcMinMax == false)
                    {
                        CalcMinMax(item);
                        item.IsCalcMinMax = true;
                    }
                    //辺最長のCube選択
                    if (length < item.Max - item.Min)
                    {
                        result = item;
                        length = item.Max - item.Min;
                    }
                }
            }
            //ピクセル数最多
            else if (select == SelectType.PixelsMax)
            {
                foreach (var item in cubes)
                {
                    if (result.Pixels.Length < item.Pixels.Length)
                        result = item;
                }
            }
            //分散最大
            else if (select == SelectType.VarianceMax)
            {
                foreach (var item in cubes)
                {
                    //分散が未計算なら計算する
                    if (item.IsCalcVariance == false)
                    {
                        item.Variance = CalcVariance(item.Pixels);
                        item.IsCalcVariance = true;
                    }

                    if (result.Variance < item.Variance)
                        result = item;
                }
            }
            //分散最大2、ヒストグラムから計算、こっちのほうが2倍位速い、大津の2値化もヒストグラムを使うから一石二鳥
            else if (select == SelectType.VarianceMax2)
            {
                foreach (var item in cubes)
                {
                    if (item.IsHistogram == false)
                    {
                        item.Histogram = MakeHistogram(item.Pixels);
                        item.IsHistogram = true;
                    }
                    if (item.IsCalcVariance == false)
                    {
                        item.Variance = CalcVariance2(item.Histogram, item.Pixels.Length);
                        item.IsCalcVariance = true;
                    }
                    if (result.Variance < item.Variance)
                        result = item;
                }
            }

            return result;
        }
        #region 分散を求める
        //分散
        private double CalcVariance(byte[] pixels)
        {
            var myBag = new ConcurrentBag<long>();
            int rangeSize = pixels.Length / Environment.ProcessorCount;
            if (rangeSize < Environment.ProcessorCount) rangeSize = pixels.Length;
            var partition = Partitioner.Create(0, pixels.Length, rangeSize);
            Parallel.ForEach(partition,
                (range) =>
                {
                    long subtotal = 0;//スレッドごとの小計用
                    for (int i = range.Item1; i < range.Item2; i++)
                    {
                        subtotal += pixels[i] * pixels[i];
                    }
                    myBag.Add(subtotal);//排他処理で追加
                });
            double average = MyAverage(pixels);//平均値取得

            //分散 = 2乗の平均 - 平均の2乗
            return (myBag.Sum() / (double)pixels.Length) - (average * average);
        }
        //分散2、ヒストグラムから計算
        private double CalcVariance2(int[] histogram, int pixelsCount)
        {
            long aTotal = 0;
            long bTotal = 0;
            long temp;
            for (int i = 0; i < histogram.Length; i++)
            {
                temp = i * histogram[i];
                aTotal += temp;
                bTotal += temp * i;
            }
            double averageSquare = aTotal / (double)pixelsCount;//平均
            averageSquare *= averageSquare;//平均の2乗
            double squareAverage = bTotal / (double)pixelsCount;//2乗の平均
            return squareAverage - averageSquare;//分散 = 2乗の平均 - 平均の2乗
        }

        //平均値
        private double MyAverage(byte[] pixels)
        {
            ConcurrentBag<long> myBag = new ConcurrentBag<long>();
            Parallel.ForEach(Partitioner.Create(0, pixels.Length),
                (range) =>
                {
                    long subtotal = 0;
                    for (int i = range.Item1; i < range.Item2; i++)
                    {
                        subtotal += pixels[i];
                    }
                    myBag.Add(subtotal);
                });
            return myBag.Sum() / (double)pixels.Length;
        }
        #endregion 分散を求める
        #endregion 分割するCube選択



        #region 選択されたCubeを2分割
        private int GetThreshold(Cube cube, SplitType split)
        {
            int threshold = 1;
            //辺の中央をしきい値にする
            if (split == SplitType.SideCenter)
            {
                if (cube.IsCalcMinMax == false) CalcMinMax(cube);
                int mid = (int)((cube.Max + cube.Min) / 2.0);
                threshold = mid;
            }

            //中央値をしきい値にする
            else if (split == SplitType.Median)
            {
                if (cube.IsCalcPixelsSorted == false)
                {
                    //Array.Sort(cube.Pixels);//これだと元のPixelの順番が変わってしまう
                    //ので新たに別の配列にコピーしてソート
                    cube.SortedPixels = new byte[cube.Pixels.Length];
                    Array.Copy(cube.Pixels, cube.SortedPixels, cube.Pixels.Length);
                    Array.Sort(cube.SortedPixels);
                    IsCalcPixelsSorted = true;
                }
                int mid = cube.SortedPixels[cube.SortedPixels.Length / 2];
                threshold = mid;
            }

            //大津の2値化
            //X ＝ a画素割合＊b画素割合＊(a平均輝度 - b平均輝度)^2
            else if (split == SplitType.Ootu)
            {
                //ヒストグラムがなければ作成
                if (cube.IsHistogram == false)
                {
                    cube.Histogram = MakeHistogram(cube.Pixels);
                    //cube.Histogram = MakeHistogram2(cube.Pixels);
                    cube.IsHistogram = true;
                }
                //MinMax計算
                if (cube.IsCalcMinMax == false)
                {
                    CalcMinMax(cube);
                    cube.IsCalcMinMax = true;
                }
                //大津の2値化を使って得られるしきい値で2分割
                double maxX = 0;
                //int threshold = 1;
                for (int i = cube.Min; i < cube.Max; i++)
                {
                    int aCount = CountHistogram(cube.Histogram, cube.Min, i);
                    int bCount = CountHistogram(cube.Histogram, i + 1, cube.Max);
                    double aRate = aCount / (double)cube.Pixels.Length;//a画素割合
                    double bRate = bCount / (double)cube.Pixels.Length;//b画素割合
                    double aKido = AverageHistogram(cube.Histogram, cube.Min, i, aCount);//a平均輝度
                    double bKido = AverageHistogram(cube.Histogram, i + 1, cube.Max, bCount);//b平均輝度
                    double X = aRate * bRate * ((aKido - bKido) * (aKido - bKido));//分離度
                    if (maxX < X)
                    {
                        maxX = X;
                        threshold = i;
                    }
                }

            }

            //Kittlerの方法でしきい値を求める
            //E = A画素割合 * Log10(A分散 / A画素割合) + B画素割合 * Log10(B分散 / B画素割合)
            //Eが最小になるしきい値
            else if (split == SplitType.Kittler)
            {
                //ヒストグラムがなければ作成
                if (cube.IsHistogram == false)
                {
                    cube.Histogram = MakeHistogram(cube.Pixels);
                    cube.IsHistogram = true;
                }
                //MinMaxが未計算なら計算
                if (cube.IsCalcMinMax == false)
                {
                    CalcMinMax(cube);
                    cube.IsCalcMinMax = true;
                }
                double min = double.MaxValue;
                double aError, bError;
                for (int i = cube.Min; i < cube.Max; i++)
                {
                    aError = KittlerSub(cube.Histogram, cube.Min, i, cube.Pixels.Length);
                    bError = KittlerSub(cube.Histogram, i + 1, cube.Max, cube.Pixels.Length);
                    var E = aError + bError;
                    if (E < min)
                    {
                        min = E;
                        threshold = i;
                    }
                }
            }
            return threshold;

        }

        //しきい値でCubeを2分割
        private (Cube cubeA, Cube cubeB) SplitCube(Cube cube, SplitType split)
        {
            //しきい値を求める
            int threshold = GetThreshold(cube, split);

            var aPix = new List<byte>();
            var bPix = new List<byte>();
            //しきい値で2分割
            foreach (var item in cube.Pixels)
            {
                //しきい値未満と以上で振り分ける、これだとグレースケールを2色にすると63,191になる
                //しきい値以下とそれ以外で振り分ける、これだとグレースケールを2色にすると64，192になる
                if (item > threshold)
                    aPix.Add(item);
                else
                    bPix.Add(item);
            }
            return (new Cube(aPix.ToArray()), new Cube(bPix.ToArray()));
        }
        #endregion 分割

        private void CalcMinMax(Cube cube)
        {
            byte min = byte.MaxValue;
            byte max = byte.MinValue;
            foreach (var item in cube.Pixels)
            {
                if (min > item) min = item;
                if (max < item) max = item;
            }
            cube.Min = min; cube.Max = max;
            cube.IsCalcMinMax = true;
        }

        #region 色取得、Cubesから色の抽出
        public List<Color> GetColors(ColorSelectType type)
        {
            var colors = new List<Color>();
            if (type == ColorSelectType.Average)
            {
                colors = GetColorsAverage();
            }
            else if (type == ColorSelectType.Core)
            {
                colors = GetColorsCore();
            }
            else if (type == ColorSelectType.Median)
            {
                colors = GetColorsMedian();
            }
            return colors;
        }
        private List<Color> GetColorsAverage()
        {
            var colors = new List<Color>();
            foreach (var cube in Cubes)
            {
                long total = 0;
                foreach (var pixel in cube.Pixels)
                {
                    total += pixel;
                }
                byte v = (byte)Math.Round((double)total / cube.Pixels.Length, MidpointRounding.AwayFromZero);
                colors.Add(Color.FromRgb(v, v, v));
            }
            return colors;
        }

        private List<Color> GetColorsCore()
        {
            var colors = new List<Color>();
            foreach (var cube in Cubes)
            {
                if (cube.IsCalcMinMax == false) CalcMinMax(cube);
                byte v = (byte)Math.Round((cube.Max + cube.Min) / 2.0, MidpointRounding.AwayFromZero);
                colors.Add(Color.FromRgb(v, v, v));
            }
            return colors;
        }

        //中央値、ソートして中央値のIndexの値
        private List<Color> GetColorsMedian()
        {
            var colors = new List<Color>();
            foreach (var cube in Cubes)
            {
                int mid = ((cube.Pixels.Length + 1) / 2) - 1;//+1して2で割っているのは四捨五入、-1してるのは配列のインデックスは0からカウントだから
                if (cube.IsCalcPixelsSorted == false)
                {
                    cube.SortedPixels = new byte[cube.Pixels.Length];
                    Array.Copy(cube.Pixels, cube.SortedPixels, cube.Pixels.Length);
                    Array.Sort(cube.SortedPixels);
                    cube.IsCalcPixelsSorted = true;
                }
                var v = cube.SortedPixels[mid];
                colors.Add(Color.FromRgb(v, v, v));
            }
            return colors;
        }
        #endregion

        #region ヒストグラム
        //BitmapSourceからヒストグラム作成、PixelFormatがGray8専用
        private int[] MakeHistogram(BitmapSource source)
        {
            int w = source.PixelWidth;
            int h = source.PixelHeight;
            int stride = w;
            byte[] pixels = new byte[h * w];
            source.CopyPixels(pixels, stride, 0);
            return MakeHistogram(pixels);
        }
        //byte配列からヒストグラム作成、PixelFormatがGray8専用
        private int[] MakeHistogram(byte[] pixels)
        {
            int[] histogram = new int[256];
            for (int i = 0; i < pixels.Length; ++i)
            {
                histogram[pixels[i]]++;
            }
            return histogram;
        }



        /// <summary>
        /// ヒストグラムの指定範囲の誤差を返す、誤差 = 要素の比率 * Log10(分散 / 要素の比率)
        /// </summary>
        /// <param name="histogram"></param>
        /// <param name="begin">範囲の開始点</param>
        /// <param name="end">範囲の終わり(未満なので、100指定なら99まで計算する)</param>
        /// <returns></returns>
        private double KittlerSub(int[] histogram, int begin, int end, int pixelsCount)
        {
            double varp = HistogramVariance(histogram, begin, end);//分散
            if (double.IsNaN(varp) || varp == 0)
                //分散が計算不能or0なら対象外になるように、大きな値(1.0)を返す
                return 1.0;
            else
            {
                double ratio = CountHistogram(histogram, begin, end);
                ratio /= pixelsCount;//画素数比率
                return ratio * Math.Log10(varp / ratio);
            }
        }

        /// <summary>
        /// ヒストグラムの指定範囲の分散を計算
        /// </summary>
        /// <param name="begin">範囲の始まり</param>
        /// <param name="end">範囲の終わり(未満なので、100指定なら99まで計算する)</param>
        /// <param name="count">範囲の画素数</param>
        /// <param name="average">範囲の平均値</param>
        /// <returns></returns>
        private double HistogramVariance(int[] histogram, int begin, int end)
        {
            long squareTotal = 0;
            long aveTotal = 0;
            long count = 0;//要素数
            for (long i = begin; i <= end; i++)
            {
                squareTotal += i * i * histogram[i];//2乗の累計
                aveTotal += i * histogram[i];
                count += histogram[i];
            }
            //平均値
            double average = aveTotal / (double)count;
            //分散 = 2乗の平均 - 平均値の2乗
            return (squareTotal / (double)count) - (average * average);
        }
        private double HistogramVariance(int[] histogram, int begin, int end, double average)
        {
            long squareTotal = 0;
            long count = 0;//要素数
            for (long i = begin; i <= end; i++)
            {
                squareTotal += i * i * histogram[i];//2乗の累計            
                count += histogram[i];
            }
            //分散 = 2乗の平均 - 平均値の2乗
            return (squareTotal / (double)count) - (average * average);
        }

        /// <summary>
        /// ヒストグラムから指定範囲の平均輝度値
        /// </summary>
        /// <param name="histogram">int型配列のヒストグラム</param>
        /// <param name="begin">範囲の始まり</param>
        /// <param name="end">範囲の終わり(未満まで計算する、100指定なら99まで計算する)</param>
        /// <param name="count">範囲内の画素数</param>
        /// <returns></returns>
        private double AverageHistogram(int[] histogram, int begin, int end, int count)
        {
            long total = 0;
            for (long i = begin; i <= end; i++)
            {
                total += i * histogram[i];
            }
            return total / (double)count;
        }

        /// <summary>
        /// ヒストグラムから指定範囲のピクセルの個数
        /// </summary>
        /// <param name="histogram">int型配列のヒストグラム</param>
        /// <param name="begin">範囲の始まり</param>
        /// <param name="end">範囲の終わり(未満まで計算する、100指定なら99まで計算する)</param>
        /// <returns></returns>
        private int CountHistogram(int[] histogram, int begin, int end)
        {
            int count = 0;
            for (int i = begin; i <= end; i++)
            {
                count += histogram[i];
            }
            return count;
        }


        #endregion ヒストグラム

    }

    public class MyData
    {
        public SolidColorBrush Brush { get; set; }
        public string ColorCode { get; set; }
        public byte GrayScaleValue { get; set; }

        public MyData(Color color)
        {
            Brush = new SolidColorBrush(color);
            ColorCode = color.ToString();
            GrayScaleValue = color.R;
        }
    }


    public class MyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool b = (bool)value;
            return !b;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    //Cube選択タイプ
    public enum SelectType
    {
        SideLong = 1,//辺最長
        PixelsMax,//ピクセル数最多
        //VolumeMax,//体積最大
        VarianceMax,//分散最大
        VarianceMax2,//分散最大、別の計算方法
    }
    //分割タイプ
    public enum SplitType
    {
        SideCenter = 1,//辺の中央
        Median,//中央値
        Ootu,//大津の2値化
        Kittler,//Kittlerの方法
    }
    //Cubeからの色選択タイプ
    public enum ColorSelectType
    {
        Average = 1,//ピクセルの平均
        Core,//Cube中心
        Median,//RGB中央値

    }
}