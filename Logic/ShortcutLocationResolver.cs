using System;

namespace SCtoolGui
{
    /// <summary>作成するショートカットの位置。SDK 非依存の自前 Flags 型。</summary>
    [Flags]
    public enum ShortcutChoice
    {
        None = 0,
        Desktop = 1,
        StartMenu = 2,
    }

    /// <summary>ウィザードのチェック状態をショートカット位置に畳む純粋ロジック。</summary>
    public static class ShortcutLocationResolver
    {
        public static ShortcutChoice Resolve(bool desktop, bool startMenu)
        {
            var c = ShortcutChoice.None;
            if (desktop) c |= ShortcutChoice.Desktop;
            if (startMenu) c |= ShortcutChoice.StartMenu;
            return c;
        }

        public static bool HasAny(ShortcutChoice c) => c != ShortcutChoice.None;
    }
}
