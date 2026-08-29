namespace SCtoolGui
{
    /// <summary>
    /// ウィンドウ外枠(RECT)から、実際にキャプチャする範囲を計算する純粋ロジック。
    /// 撮影(SaveWindowCaptureWithExif)と一時プレビュー(SavePreviewOnly)の両方で
    /// 同じ計算を使うため、ScreenCapture から切り出してある。
    /// </summary>
    public static class CaptureCropCalculator
    {
        /// <summary>Windowsのシステム枠（アクセントカラー枠）を除外するためのカット量(px)。</summary>
        public const int SystemBorderCut = 1;

        /// <summary>
        /// キャプチャ範囲の計算結果。
        /// CaptureWidth/CaptureHeight は枠を除いた全体サイズ（プレビュー用）、
        /// FinalHeight はさらに上端カット(topCutPixels)を引いた保存用の高さ。
        /// IsValid が false の場合、ウィンドウサイズ取得失敗かカット量過多で撮影不能。
        /// </summary>
        public record Result(int CaptureWidth, int CaptureHeight, int FinalHeight, bool IsValid);

        /// <summary>ウィンドウ外枠の座標とトップカット量から、キャプチャ範囲を計算する。</summary>
        public static Result Compute(int left, int top, int right, int bottom, int topCutPixels)
        {
            int originalWidth = right - left;
            int originalHeight = bottom - top;
            int captureWidth = originalWidth - (SystemBorderCut * 2);
            int captureHeight = originalHeight - (SystemBorderCut * 2);
            int finalHeight = captureHeight - topCutPixels;

            return new Result(captureWidth, captureHeight, finalHeight, captureWidth > 0 && finalHeight > 0);
        }
    }
}
