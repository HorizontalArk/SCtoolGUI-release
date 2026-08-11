using System.IO;
using SCtoolGui;

namespace SCtoolGui.Tests
{
    public class IconIcoWriterTests
    {
        [Fact]
        public void PNGから複数解像度のicoを書き出せる()
        {
            string src = Path.Combine(Path.GetTempPath(), $"src_{System.Guid.NewGuid():N}.png");
            string dst = Path.Combine(Path.GetTempPath(), $"dst_{System.Guid.NewGuid():N}.ico");
            try
            {
                WriteTestPng(src, 64, 48); // 非正方でもよい
                bool ok = IconIcoWriter.TryWriteIco(src, dst, out string err);
                Assert.True(ok, err);
                Assert.True(File.Exists(dst));

                using var fs = File.OpenRead(dst);
                var head = new byte[6];
                fs.Read(head, 0, 6);
                // ICONDIR: reserved=0, type=1(icon), count>=1
                Assert.Equal(0, head[0] | head[1]);
                Assert.Equal(1, head[2] | (head[3] << 8));
                int count = head[4] | (head[5] << 8);
                Assert.True(count >= 1);
            }
            finally
            {
                if (File.Exists(src)) File.Delete(src);
                if (File.Exists(dst)) File.Delete(dst);
            }
        }

        // 32bit BGRA の単色 PNG を書く（テスト補助）
        private static void WriteTestPng(string path, int w, int h)
        {
            var rt = new System.Windows.Media.Imaging.RenderTargetBitmap(
                w, h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            var dv = new System.Windows.Media.DrawingVisual();
            using (var dc = dv.RenderOpen())
                dc.DrawRectangle(System.Windows.Media.Brushes.Crimson, null,
                    new System.Windows.Rect(0, 0, w, h));
            rt.Render(dv);
            var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
            enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rt));
            using var fs = File.Create(path);
            enc.Save(fs);
        }
    }
}
