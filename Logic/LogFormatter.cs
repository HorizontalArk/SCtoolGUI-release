namespace SCtoolGui
{
    /// <summary>ログの重大度。情報は無印、それ以外はタグを付ける。</summary>
    public enum LogLevel
    {
        /// <summary>通常の情報。プレフィックスなし。</summary>
        Info,
        /// <summary>操作の成功。</summary>
        Success,
        /// <summary>警告（操作の中断・スキップ・拒否・注意喚起を含む）。</summary>
        Warning,
        /// <summary>エラー（例外・失敗）。</summary>
        Error,
    }

    /// <summary>
    /// ログ文言の整形を一元化する純粋関数群。
    /// プレフィックス・対象名の囲みをここに集約し、各所のベタ書きによる表記ゆれを防ぐ。
    /// </summary>
    public static class LogFormatter
    {
        /// <summary>重大度に応じたプレフィックスを付けて整形する。情報は無印でそのまま返す。</summary>
        public static string Format(LogLevel level, string message) => level switch
        {
            LogLevel.Success => $"[成功] {message}",
            LogLevel.Warning => $"[警告] {message}",
            LogLevel.Error => $"[エラー] {message}",
            _ => message,
        };

        /// <summary>ログ内で対象（ウィンドウ）名を示す整形。空なら未選択プレースホルダ。</summary>
        public static string Target(string name)
            => string.IsNullOrEmpty(name) ? "[対象未選択]" : $"[{name}]";
    }
}
