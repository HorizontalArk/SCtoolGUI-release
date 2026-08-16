namespace SCtoolGui
{
    /// <summary>
    /// スクショの完全なファイル名（拡張子付き）を組み立てる。
    /// 撮影本体と保存先プレビューが同じ組み立てを共有し、表示と実結果のズレを防ぐ。
    /// 可変なのは時刻部分だけなので、プレビューではプレースホルダを、撮影では実時刻を渡す。
    /// </summary>
    public static class CaptureFileName
    {
        /// <summary>保存先プレビューで時刻の代わりに見せる表記。撮影ごとに変わる部分。</summary>
        public const string TimePlaceholder = "yyyymmdd_hhmmss";

        /// <summary>ファイル名ベース（ウィンドウ名/登録名）と時刻部分を連結し、拡張子を付ける。</summary>
        public static string Build(string fileBase, string timePart)
            => $"{fileBase}_{timePart}.jpg";
    }
}
