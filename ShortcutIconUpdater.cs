using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace SCtoolGui
{
    /// <summary>SCtoolGui.lnk が置かれ得る既知の標準パスを、環境変数展開だけで決める純粋ロジック。</summary>
    public static class ShortcutIconTargets
    {
        public static IReadOnlyList<string> Resolve(string appData, string userProfile)
        {
            return new[]
            {
                Path.Combine(appData, "Microsoft", "Windows", "Start Menu", "Programs", "SCtoolGui.lnk"),
                Path.Combine(userProfile, "Desktop", "SCtoolGui.lnk"),
                Path.Combine(appData, "Microsoft", "Internet Explorer", "Quick Launch",
                             "User Pinned", "TaskBar", "SCtoolGui.lnk"),
            };
        }
    }

    /// <summary>
    /// 既知の .lnk 群の IconLocation を更新し、タスクバー等のアイコンをユーザー画像へ反映する。
    /// タスクバー表示は実行中ウィンドウの WM_SETICON ではなく .lnk の IconLocation が支配するため、
    /// ここで .lnk を書き換える。反映は即時保証せず、SHChangeNotify 通知後 次回起動/サインインで確実化。
    /// ユーザー画像から変換した .ico は %AppData%\SCtoolGui にのみ置く（配布物・リポジトリに含めない）。
    /// </summary>
    public static class ShortcutIconUpdater
    {
        private const int SHCNE_ASSOCCHANGED = 0x08000000;
        private const uint SHCNF_IDLIST = 0x0000;
        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);

        /// <summary>変換後 .ico の保存先（%AppData%\SCtoolGui\user_icon.ico）。</summary>
        public static string UserIcoPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "SCtoolGui", "user_icon.ico");

        /// <summary>ユーザー画像を .ico 化し、存在する既知 .lnk の IconLocation を更新する。</summary>
        public static void ApplyUserIcon(string userImagePath)
        {
            if (string.IsNullOrEmpty(userImagePath) || !File.Exists(userImagePath)) return;
            if (!IconIcoWriter.TryWriteIco(userImagePath, UserIcoPath, out _)) return;
            UpdateAll($"{UserIcoPath},0");
        }

        /// <summary>既知 .lnk の IconLocation を exe 埋め込み既定（app.ico）へ戻す。</summary>
        public static void ResetToDefault(string exePath)
        {
            if (string.IsNullOrEmpty(exePath)) return;
            UpdateAll($"{exePath},0");
        }

        private static void UpdateAll(string iconLocation)
        {
            var targets = ShortcutIconTargets.Resolve(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

            bool any = false;
            foreach (var lnk in targets)
            {
                if (!File.Exists(lnk)) continue; // ピン留め等していない場所はスキップ（正常系）
                try
                {
                    SetShortcutIcon(lnk, iconLocation);
                    any = true;
                }
                catch { /* 1 つ失敗しても他を試す */ }
            }
            if (any) SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
        }

        /// <summary>WScript.Shell 経由で .lnk の IconLocation を設定して保存する。</summary>
        private static void SetShortcutIcon(string lnkPath, string iconLocation)
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell")!;
            dynamic shell = Activator.CreateInstance(shellType)!;
            try
            {
                dynamic sc = shell.CreateShortcut(lnkPath);
                sc.IconLocation = iconLocation;
                sc.Save();
                Marshal.FinalReleaseComObject(sc);
            }
            finally
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }
    }
}
