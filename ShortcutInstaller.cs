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
            new Shortcuts().CreateShortcutForThisExe(locations);
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
