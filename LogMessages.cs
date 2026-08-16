using System.Globalization;
using System.Resources;

namespace SCtoolGui
{
    /// <summary>
    /// ログ文言リソース(Resources/LogMessages.resx)への型安全なアクセサ。
    /// キー名のプロパティで文言を取り、Format() で複合書式の引数を埋める。
    /// 文言はすべて resx 側に集約し、コードにはベタ書きしない（表記ゆれ防止・将来の多言語化）。
    /// </summary>
    public static class LogMessages
    {
        private static readonly ResourceManager Rm =
            new ResourceManager("SCtoolGui.Resources.LogMessages", typeof(LogMessages).Assembly);

        /// <summary>キーに対応する文言を取得。見つからなければキー名をそのまま返す（早期発見のため）。</summary>
        private static string Get(string key) =>
            Rm.GetString(key, CultureInfo.CurrentUICulture) ?? key;

        /// <summary>キーの文言を取得し、複合書式の引数を埋める。</summary>
        public static string Format(string key, params object[] args) =>
            string.Format(CultureInfo.CurrentCulture, Get(key), args);

        // 撮影
        public static string CaptureSucceeded(string target, string path) => Format(nameof(CaptureSucceeded), target, path);
        public static string CaptureTargetNotRunning(string target) => Format(nameof(CaptureTargetNotRunning), target);
        public static string CaptureTargetMinimized(string target) => Format(nameof(CaptureTargetMinimized), target);
        public static string CaptureFailed(string message) => Format(nameof(CaptureFailed), message);

        // ホットキー
        public static string HotkeyRegisterFailedInUse => Get(nameof(HotkeyRegisterFailedInUse));
        public static string HotkeyRegisterFailed => Get(nameof(HotkeyRegisterFailed));

        // 上部カット
        public static string TopCutChanged(string onOff) => Format(nameof(TopCutChanged), onOff);

        // プレビュー
        public static string PreviewNoWindow => Get(nameof(PreviewNoWindow));
        public static string PreviewMinimized => Get(nameof(PreviewMinimized));
        public static string PreviewCaptureFailed => Get(nameof(PreviewCaptureFailed));
        public static string PreviewUpdated => Get(nameof(PreviewUpdated));
        public static string PreviewUpdateError(string message) => Format(nameof(PreviewUpdateError), message);

        // クリップボード / 削除
        public static string ClipboardNoTempPreview => Get(nameof(ClipboardNoTempPreview));
        public static string ClipboardNoSavedImage => Get(nameof(ClipboardNoSavedImage));
        public static string ClipboardCopiedAuto => Get(nameof(ClipboardCopiedAuto));
        public static string ClipboardCopied => Get(nameof(ClipboardCopied));
        public static string ClipboardCopyFailed(string message) => Format(nameof(ClipboardCopyFailed), message);
        public static string FileDeleted(string fileName) => Format(nameof(FileDeleted), fileName);
        public static string FileDeleteFailed(string message) => Format(nameof(FileDeleteFailed), message);

        // ウィンドウ / 対象
        public static string ElevationRestartCanceled => Get(nameof(ElevationRestartCanceled));
        public static string WindowSwitched(string target) => Format(nameof(WindowSwitched), target);
        public static string CutSettingRestored(string target, int cut) => Format(nameof(CutSettingRestored), target, cut);
        public static string TopmostSet(string target) => Format(nameof(TopmostSet), target);
        public static string TopmostReleased(string target) => Format(nameof(TopmostReleased), target);
        public static string TopmostCanceledNotRunning(string target) => Format(nameof(TopmostCanceledNotRunning), target);
        public static string TopmostCanceledMinimized(string target) => Format(nameof(TopmostCanceledMinimized), target);
        public static string TopmostBlocked(string target, int errorCode) => Format(nameof(TopmostBlocked), target, errorCode);
        public static string NoTargetSelected => Get(nameof(NoTargetSelected));

        // 画像フォルダ / 呼び名
        public static string FolderMoved(string oldName, string newName) => Format(nameof(FolderMoved), oldName, newName);
        public static string FolderMoveFailed(string message) => Format(nameof(FolderMoveFailed), message);
        public static string FolderMoveSkippedConflict(string newName) => Format(nameof(FolderMoveSkippedConflict), newName);
        public static string TargetRenamed(string oldName, string newName) => Format(nameof(TargetRenamed), oldName, newName);

        // 設定 / アイコン
        public static string SettingsSaveFailed => Get(nameof(SettingsSaveFailed));
        public static string SettingsUpdated => Get(nameof(SettingsUpdated));
        public static string IconTaskbarConvertFailed(string detail) => Format(nameof(IconTaskbarConvertFailed), detail);
        public static string FolderToOpenMissing => Get(nameof(FolderToOpenMissing));

        // アップデート
        public static string UpdateSkippedNotInstalled => Get(nameof(UpdateSkippedNotInstalled));
        public static string UpdateChecking => Get(nameof(UpdateChecking));
        public static string UpdateAvailable => Get(nameof(UpdateAvailable));
        public static string UpdateUpToDate => Get(nameof(UpdateUpToDate));
        public static string UpdateCheckFailed => Get(nameof(UpdateCheckFailed));
        public static string UpdateDownloading => Get(nameof(UpdateDownloading));
    }
}
