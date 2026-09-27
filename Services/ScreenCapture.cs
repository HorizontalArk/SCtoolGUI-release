using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Reflection;
using System.IO;

namespace SCtoolGui
{
    public static class ScreenCapture
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

        public struct RECT { public int Left, Top, Right, Bottom; }
        private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

        /// <summary>対象が前面に来るのを待つ最大時間(ms)。これを超えたら撮影を中止する。</summary>
        private const int ForegroundTimeoutMs = 1000;

        /// <summary>前面になったか確認する間隔(ms)。</summary>
        private const int ForegroundPollIntervalMs = 10;

        /// <summary>
        /// 前面に来た直後の描画完了を待つ時間(ms)。
        /// アニメーション付きで復帰するアプリのため、確認後にもわずかに待つ。
        /// </summary>
        private const int PostForegroundSettleMs = 80;

        /// <summary>
        /// 対象ウィンドウを前面に出し、実際に前面になるまで待つ。
        ///
        /// SetForegroundWindow はOSの仕様で失敗することがある（他アプリがフォアグラウンド
        /// ロックを持っている等）。戻り値を無視して固定時間だけ待つと、前面に来ないまま
        /// 撮影して「別のウィンドウが写った画像」を保存してしまうため、必ず確認する。
        /// </summary>
        /// <returns>前面にできた場合は true。時間内に前面にならなければ false。</returns>
        private static bool TryBringToForeground(IntPtr hwnd)
        {
            if (ForegroundWindowHelper.GetForeground() == hwnd) return true;

            // 最大化状態を通常サイズへ戻してしまわないよう ShowWindow(SW_RESTORE) は使わない。
            // （自ウィンドウ復帰の BringToolToForeground と同じ手法。ForegroundWindowHelperに共通化してある）
            ForegroundWindowHelper.TryForegroundOnce(hwnd);

            // 固定待ちではなく実際に前面になるまで待つ。多くの場合250msより短く済む。
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < ForegroundTimeoutMs)
            {
                if (ForegroundWindowHelper.GetForeground() == hwnd)
                {
                    System.Threading.Thread.Sleep(PostForegroundSettleMs);
                    return true;
                }
                System.Threading.Thread.Sleep(ForegroundPollIntervalMs);
            }

            return ForegroundWindowHelper.GetForeground() == hwnd;
        }

        /// <summary>ウィンドウの外枠（DWMの拡張フレーム境界）を取得する。</summary>
        private static RECT GetWindowBounds(IntPtr hwnd)
        {
            DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out RECT rect, Marshal.SizeOf(typeof(RECT)));
            return rect;
        }

        /// <summary>
        /// JPEG の保存品質(1-100)。
        /// 既定値は75で、スクリーンショットのような文字の多い画像では
        /// 輪郭にノイズが出るため引き上げている。
        /// </summary>
        private const long JpegQuality = 95;

        /// <summary>JPEGエンコーダ。取得に失敗した場合は null（呼び出し側で既定保存にフォールバック）。</summary>
        private static readonly ImageCodecInfo? JpegEncoder =
            ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);

        /// <summary>品質を指定してJPEGで保存する。</summary>
        private static void SaveJpeg(Bitmap bitmap, string path)
        {
            if (JpegEncoder == null)
            {
                // エンコーダを取得できない環境では既定の品質で保存する
                bitmap.Save(path, ImageFormat.Jpeg);
                return;
            }

            using var parameters = new EncoderParameters(1);
            // Encoder は System.Text.Encoder と名前が衝突するため完全修飾する
            using var quality = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, JpegQuality);
            parameters.Param[0] = quality;

            bitmap.Save(path, JpegEncoder, parameters);
        }

        /// <summary>
        /// PNG で保存し、撮影日時を eXIf チャンクとして書き込む。
        /// GDI+ の PNG エンコーダは PropertyItem を捨てるため、エンコード後に自前で差し込む。
        /// </summary>
        private static void SavePngWithExif(Bitmap bitmap, string path)
        {
            using var ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Png);
            File.WriteAllBytes(path, PngDateTakenWriter.InsertDateTaken(ms.ToArray(), DateTime.Now));
        }

        /// <summary>
        /// 対象ウィンドウを撮影し、上部カット後の本番画像を撮影日時付きで savePath に保存する。
        /// 一時プレビュー(previewPath)は保存形式に関わらず JPEG のまま。
        /// </summary>
        public static void SaveWindowCaptureWithExif(IntPtr hwnd, string savePath, string previewPath, int topCutPixels = 0, string? saveFormat = CaptureFormat.Jpeg)
        {
            // 前面化を先に行う。前面に来る際にウィンドウが移動・復元されることがあるため、
            // 座標の取得はその後にしないと古い位置を撮ってしまう。
            if (!TryBringToForeground(hwnd))
            {
                throw new Exception(
                    "対象ウィンドウを前面に表示できなかったため、撮影を中止しました。\n" +
                    "別のウィンドウが写り込むのを防ぐためです。対象をクリックしてから再試行してください。");
            }

            RECT rect = GetWindowBounds(hwnd);

            // 左右・上下からシステム枠分を削った実際のキャプチャサイズを計算する
            var crop = CaptureCropCalculator.Compute(rect.Left, rect.Top, rect.Right, rect.Bottom, topCutPixels);
            if (!crop.IsValid) throw new Exception("ウィンドウサイズが正しく取得できません、またはカット後のサイズが不正です。");

            using (Bitmap fullBmp = new Bitmap(crop.CaptureWidth, crop.CaptureHeight))
            using (Graphics g = Graphics.FromImage(fullBmp))
            {
                // 座標をシステム枠分だけ内側にずらして画面をコピーする
                int cut = CaptureCropCalculator.SystemBorderCut;
                g.CopyFromScreen(rect.Left + cut, rect.Top + cut, 0, 0, new Size(crop.CaptureWidth, crop.CaptureHeight), CopyPixelOperation.SourceCopy);

                SaveJpeg(fullBmp, previewPath);

                using (Bitmap cutBmp = new Bitmap(crop.CaptureWidth, crop.FinalHeight))
                using (Graphics gCut = Graphics.FromImage(cutBmp))
                {
                    gCut.DrawImage(fullBmp, new Rectangle(0, 0, crop.CaptureWidth, crop.FinalHeight), new Rectangle(0, topCutPixels, crop.CaptureWidth, crop.FinalHeight), GraphicsUnit.Pixel);
                    if (CaptureFormat.IsPng(saveFormat))
                    {
                        SavePngWithExif(cutBmp, savePath);
                    }
                    else
                    {
                        AddExifData(cutBmp);
                        SaveJpeg(cutBmp, savePath);
                    }
                }
            }
        }

        public static bool SavePreviewOnly(IntPtr hwnd, string previewPath)
        {
            try
            {
                // 撮影と同じく、前面に出せたことを確認してから座標を取る
                if (!TryBringToForeground(hwnd)) return false;

                RECT rect = GetWindowBounds(hwnd);

                // 一時プレビュー用のキャプチャでも同様に枠を削る(トップカットは適用しない)
                var crop = CaptureCropCalculator.Compute(rect.Left, rect.Top, rect.Right, rect.Bottom, topCutPixels: 0);
                if (!crop.IsValid) return false;

                using (Bitmap fullBmp = new Bitmap(crop.CaptureWidth, crop.CaptureHeight))
                using (Graphics g = Graphics.FromImage(fullBmp))
                {
                    int cut = CaptureCropCalculator.SystemBorderCut;
                    g.CopyFromScreen(rect.Left + cut, rect.Top + cut, 0, 0, new Size(crop.CaptureWidth, crop.CaptureHeight), CopyPixelOperation.SourceCopy);
                    SaveJpeg(fullBmp, previewPath);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void AddExifData(Bitmap bmp)
        {
            string now = DateTime.Now.ToString("yyyy:MM:dd HH:mm:ss") + "\0";
            byte[] bytes = Encoding.ASCII.GetBytes(now);
            ConstructorInfo? constructor = typeof(PropertyItem).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (constructor == null) return;
            PropertyItem prop = (PropertyItem)constructor.Invoke(null);
            prop.Id = 0x9003; prop.Type = 2; prop.Len = bytes.Length; prop.Value = bytes;
            bmp.SetPropertyItem(prop);
        }
    }
}