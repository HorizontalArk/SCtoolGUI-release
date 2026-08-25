namespace SCtoolGui
{
    /// <summary>
    /// キャプチャ／プレビュー後に、自アプリを前面へ戻すか（true）
    /// 対象ウィンドウをアクティブのままにするか（false）を決める純粋ロジック。
    /// 「統一」設定が ON のとき、プレビューはキャプチャ側の設定に追従する。
    /// </summary>
    public static class FocusRestoreLogic
    {
        /// <summary>キャプチャ後に自アプリを前面へ戻すか。</summary>
        public static bool AfterCapture(bool restoreOnCapture) => restoreOnCapture;

        /// <summary>プレビュー取得後に自アプリを前面へ戻すか。統一 ON ならキャプチャ側に追従。</summary>
        public static bool AfterPreview(bool unify, bool restoreOnCapture, bool restoreOnPreview)
            => unify ? restoreOnCapture : restoreOnPreview;
    }
}
