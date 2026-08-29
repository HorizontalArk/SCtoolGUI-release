using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SCtoolGui
{
    /// <summary>
    /// 実行ファイルのアイコンを取得してキャッシュする。
    /// ウィンドウ一覧は数秒ごとに再取得されるため、exe パス単位で1回だけ抽出する。
    /// </summary>
    public static class ProcessIconCache
    {
        private static readonly Dictionary<string, ImageSource?> _cache =
            new(StringComparer.OrdinalIgnoreCase);

        public static ImageSource? Get(string? exePath)
        {
            if (string.IsNullOrEmpty(exePath)) return null;
            if (_cache.TryGetValue(exePath, out var cached)) return cached;

            ImageSource? img = null;
            try
            {
                using var ico = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                if (ico != null)
                {
                    img = Imaging.CreateBitmapSourceFromHIcon(
                        ico.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    img?.Freeze(); // 別スレッドや長期保持でも安全にする
                }
            }
            catch { /* 権限不足・パス不正などは無視（アイコン無し） */ }

            _cache[exePath] = img;
            return img;
        }
    }
}
