using Velopack.Windows;

namespace SCtoolGui
{
    /// <summary>
    /// ウィザードの選択に従い Velopack でショートカットを作成する薄いラッパ。
    /// Velopack 非管理環境（ポータブル/開発）や未選択のときは何もしない。
    /// </summary>
    public static class ShortcutInstaller
    {
        public static void Create(ShortcutChoice choice, bool isInstalled)
        {
            if (!isInstalled) return;
            if (!ShortcutLocationResolver.HasAny(choice)) return;

            var locations = ToVelopack(choice);
            // Velopack は Desktop/StartMenuRoot を自動生成する前提で Shortcuts を旧形式(CS0618)扱いにしているが、
            // 本アプリは vpk pack --shortcuts None で自動生成を切り、初回ウィザードの選択に従って手動作成する。
            // そのためこの API を意図的に使う。将来 Velopack が削除したら代替 API へ移行する。
#pragma warning disable CS0618
            new Shortcuts().CreateShortcutForThisExe(locations);
#pragma warning restore CS0618
        }

        private static ShortcutLocation ToVelopack(ShortcutChoice choice)
        {
            var loc = ShortcutLocation.None;
            if (choice.HasFlag(ShortcutChoice.Desktop)) loc |= ShortcutLocation.Desktop;
            // 「スタートメニュー」はフォルダを作らないルート直下に置く
            if (choice.HasFlag(ShortcutChoice.StartMenu)) loc |= ShortcutLocation.StartMenuRoot;
            return loc;
        }
    }
}
