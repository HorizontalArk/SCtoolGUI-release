namespace SCtoolGui
{
    /// <summary>スクショの保存形式。設定(AppSettings.SaveFormat)には文字列で保存する。</summary>
    public static class CaptureFormat
    {
        public const string Jpeg = "Jpeg";
        public const string Png = "Png";

        /// <summary>設定値が PNG か。未知の値や空は従来どおり JPEG として扱う。</summary>
        public static bool IsPng(string? format) => format == Png;

        /// <summary>保存形式に対応する拡張子（ドット付き）。</summary>
        public static string Extension(string? format) => IsPng(format) ? ".png" : ".jpg";
    }

    /// <summary>
    /// スクショの完全なファイル名（拡張子付き）を組み立てる。
    /// 撮影本体と保存先プレビューが同じ組み立てを共有し、表示と実結果のズレを防ぐ。
    /// 可変なのは時刻部分だけなので、プレビューではプレースホルダを、撮影では実時刻を渡す。
    /// </summary>
    public static class CaptureFileName
    {
        /// <summary>保存先プレビューで時刻の代わりに見せる表記。撮影ごとに変わる部分。</summary>
        public const string TimePlaceholder = "yyyymmdd_hhmmss";

        /// <summary>ファイル名ベース（ウィンドウ名/登録名）と時刻部分を連結し、保存形式の拡張子を付ける。</summary>
        public static string Build(string fileBase, string timePart, string? format = CaptureFormat.Jpeg)
            => $"{fileBase}_{timePart}{CaptureFormat.Extension(format)}";
    }
}
