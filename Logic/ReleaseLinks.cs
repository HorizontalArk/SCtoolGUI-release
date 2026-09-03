namespace SCtoolGui
{
    /// <summary>
    /// バージョン情報から外部リンク(GitHub Releaseページなど)を組み立てる純粋ロジック。
    /// アップデート通知に「詳細はこちら」リンクを付けるために使う。
    /// </summary>
    public static class ReleaseLinks
    {
        /// <summary>
        /// 指定バージョンのGitHub Releaseページ URL。
        /// タグ名は "v" + バージョン(例: "1.2.0" → "v1.2.0")の前提で組み立てる。
        /// </summary>
        public static string ReleasePageUrl(string repoUrl, string version)
            => $"{repoUrl.TrimEnd('/')}/releases/tag/v{version}";
    }
}
