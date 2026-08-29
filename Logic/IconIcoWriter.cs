using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SCtoolGui
{
    /// <summary>
    /// 任意画像（png/jpg/bmp/ico）を、複数解像度の PNG フレームを内包する .ico に変換して書き出す。
    /// .lnk の IconLocation はタスクバー表示に .ico を要求するため、ユーザー選択画像をこの形式へ変換する。
    /// System.Drawing に依存せず、WPF で各サイズにデコードした PNG を ICO コンテナに手書きする。
    /// </summary>
    public static class IconIcoWriter
    {
        // 生成するアイコンのサイズ（px）。タスクバー/一覧/大アイコン向け。
        private static readonly int[] Sizes = { 16, 32, 48, 256 };

        public static bool TryWriteIco(string sourceImagePath, string destIcoPath, out string error)
        {
            error = "";
            try
            {
                var src = new BitmapImage();
                src.BeginInit();
                src.CacheOption = BitmapCacheOption.OnLoad;
                src.UriSource = new Uri(sourceImagePath);
                src.EndInit();

                var pngFrames = new List<byte[]>();
                foreach (int size in Sizes)
                    pngFrames.Add(RenderPng(src, size));

                Directory.CreateDirectory(Path.GetDirectoryName(destIcoPath)!);
                using var fs = File.Create(destIcoPath);
                WriteIcoContainer(fs, pngFrames);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>元画像をアスペクト保持で size×size の正方に中央配置し、PNG バイト列にする。</summary>
        private static byte[] RenderPng(BitmapSource src, int size)
        {
            var target = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                double scale = Math.Min((double)size / src.PixelWidth, (double)size / src.PixelHeight);
                double w = src.PixelWidth * scale, h = src.PixelHeight * scale;
                dc.DrawImage(src, new Rect((size - w) / 2, (size - h) / 2, w, h));
            }
            target.Render(visual);

            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(target));
            using var ms = new MemoryStream();
            enc.Save(ms);
            return ms.ToArray();
        }

        /// <summary>ICONDIR + ICONDIRENTRY×N + 各 PNG データ を書く。</summary>
        private static void WriteIcoContainer(Stream fs, List<byte[]> pngFrames)
        {
            using var w = new BinaryWriter(fs);
            // ICONDIR
            w.Write((ushort)0);              // reserved
            w.Write((ushort)1);              // type = 1 (icon)
            w.Write((ushort)pngFrames.Count);

            // 各 PNG データはヘッダ群の直後から並ぶ
            int offset = 6 + 16 * pngFrames.Count;
            for (int i = 0; i < pngFrames.Count; i++)
            {
                int size = Sizes[i];
                byte[] png = pngFrames[i];
                // ICONDIRENTRY
                w.Write((byte)(size >= 256 ? 0 : size)); // width（256 は 0 表記）
                w.Write((byte)(size >= 256 ? 0 : size)); // height
                w.Write((byte)0);   // color count
                w.Write((byte)0);   // reserved
                w.Write((ushort)1); // planes
                w.Write((ushort)32);// bit count
                w.Write((uint)png.Length);
                w.Write((uint)offset);
                offset += png.Length;
            }
            foreach (var png in pngFrames)
                w.Write(png);
        }
    }
}
