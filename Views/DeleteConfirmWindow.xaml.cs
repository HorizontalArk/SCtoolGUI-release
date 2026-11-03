using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace SCtoolGui
{
    /// <summary>削除対象の画像をプレビュー付きで確認するダイアログ。「削除」で閉じた場合のみ DialogResult=true。</summary>
    public partial class DeleteConfirmWindow : Window
    {
        public DeleteConfirmWindow(string filePath)
        {
            InitializeComponent();

            TxtFileName.Text = Path.GetFileName(filePath);
            TxtFileName.ToolTip = filePath;

            try
            {
                var info = new FileInfo(filePath);
                var (width, height) = ReadPixelSize(filePath);
                ImgTarget.Source = LoadThumbnail(filePath, height);
                TxtFileInfo.Text = $"{width} × {height} ・ {FormatSize(info.Length)} ・ {info.LastWriteTime:yyyy/MM/dd HH:mm:ss}";
            }
            catch (Exception)
            {
                // 読めなくても削除自体の確認はできるようにする
                TxtFileInfo.Text = "プレビューを表示できませんでした";
            }
            TxtFileInfo.ToolTip = Path.GetDirectoryName(filePath);

            Loaded += (_, _) => BtnCancel.Focus();
        }

        /// <summary>
        /// ファイルを掴まないよう OnLoad で読み切り、表示サイズ程度に縮小デコードする
        /// （大きな画像でもメモリと表示時間を抑えるため）。
        /// </summary>
        private static BitmapImage LoadThumbnail(string filePath, int originalHeight)
        {
            const int MaxDecodeHeight = 520; // 表示枠(260px)の2倍。高DPIでも粗くならない程度
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            if (originalHeight > MaxDecodeHeight) bitmap.DecodePixelHeight = MaxDecodeHeight;
            bitmap.UriSource = new Uri(filePath);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        /// <summary>画素データをデコードせず、元画像の幅・高さだけを読む。</summary>
        private static (int Width, int Height) ReadPixelSize(string filePath)
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            var frame = decoder.Frames[0];
            return (frame.PixelWidth, frame.PixelHeight);
        }

        private static string FormatSize(long bytes) => bytes switch
        {
            >= 1024 * 1024 => $"{bytes / 1024.0 / 1024.0:0.0} MB",
            >= 1024 => $"{bytes / 1024.0:0} KB",
            _ => $"{bytes} B",
        };

        private void BtnDelete_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    }
}
