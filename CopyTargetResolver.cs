namespace SCtoolGui
{
    /// <summary>クリップボードにコピーする対象の種別。</summary>
    public enum CopyTarget
    {
        /// <summary>撮影前の確認用に表示している一時プレビュー画像。</summary>
        TempPreview,
        /// <summary>直近に撮影・保存した本番画像。</summary>
        LastSaved,
    }

    /// <summary>
    /// コピー対象の種別と各画像の存在状況から、実際にコピーすべきパスを決める。
    /// ファイル存在判定は呼び出し側で行い、結果だけ渡すことで純粋関数に保つ。
    /// </summary>
    public static class CopyTargetResolver
    {
        /// <summary>
        /// 指定した対象に対応するパスを返す。対象が存在しない場合は null。
        /// </summary>
        public static string? Resolve(
            CopyTarget target,
            string tempPreviewPath, bool tempPreviewExists,
            string lastSavedPath, bool lastSavedExists)
        {
            return target switch
            {
                CopyTarget.TempPreview => tempPreviewExists ? tempPreviewPath : null,
                CopyTarget.LastSaved => lastSavedExists ? lastSavedPath : null,
                _ => null,
            };
        }

        /// <summary>設定文字列を種別に変換する。未知の値は LastSaved 扱い。</summary>
        public static CopyTarget Parse(string source)
            => source == "TempPreview" ? CopyTarget.TempPreview : CopyTarget.LastSaved;
    }
}
