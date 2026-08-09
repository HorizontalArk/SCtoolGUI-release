using System.Windows;

namespace SCtoolGui
{
    /// <summary>
    /// アプリの UI テーマ（System=OS追従 / Light / Dark）を適用する。
    /// WPF 標準の Fluent テーマ（ThemeMode）を切り替える。
    /// </summary>
    public static class ThemeManager
    {
        public static void Apply(string? theme)
        {
            if (Application.Current == null) return;

            Application.Current.ThemeMode = theme switch
            {
                "Light" => ThemeMode.Light,
                "Dark" => ThemeMode.Dark,
                _ => ThemeMode.System,
            };
        }
    }
}
