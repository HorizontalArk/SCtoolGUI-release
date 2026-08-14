namespace SCtoolGui
{
    /// <summary>
    /// スクショのファイル名ベース（拡張子・タイムスタンプを除く部分）を決める。
    /// 設定に応じて登録名か実ウィンドウタイトルを使い分ける。
    /// </summary>
    public static class FileBaseNameResolver
    {
        /// <summary>
        /// ファイル名ベースを返す。
        /// useWindowTitle が true かつ windowTitle が非空なら、タイトルを安全化して使う。
        /// それ以外（OFF・タイトルが空）は、既に安全化済みの登録名をそのまま返す。
        /// </summary>
        public static string Resolve(bool useWindowTitle, string windowTitle, string registeredName)
        {
            if (useWindowTitle && !string.IsNullOrEmpty(windowTitle))
            {
                return FileNameUtil.ToSafeName(windowTitle);
            }
            return registeredName;
        }
    }
}
