namespace SCtoolGui
{
    /// <summary>
    /// コピーボタン本体の有効/無効を、既定のコピー対象と保存画像の有無から決める純粋ロジック。
    ///
    /// コピーボタン本体は「既定のコピー対象(<see cref="AppSettings.CopySource"/>)」に従って動く。
    /// 既定が LastSaved（最後に保存した画像）のときだけ保存画像が必要で、無ければ実行できない。
    /// プレビュー系（TempPreview / FreshPreview）は対象ウィンドウがあれば動くため、保存画像が
    /// 無くても有効にする（FreshPreview は撮り直すので特に保存不要）。
    /// </summary>
    public static class CopyButtonState
    {
        /// <summary>
        /// コピーボタン本体を有効にすべきか。
        /// 既定対象が LastSaved かつ保存画像が無いときだけ false、それ以外は true。
        /// </summary>
        public static bool IsMainCopyEnabled(CopyTarget defaultTarget, bool hasLastCapture)
            => defaultTarget != CopyTarget.LastSaved || hasLastCapture;
    }
}
