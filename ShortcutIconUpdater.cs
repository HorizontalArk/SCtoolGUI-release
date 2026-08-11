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

        /// <summary>
        /// ユーザー画像を .ico 化し、存在する既知 .lnk の IconLocation を更新する。
        /// .ico 変換に成功したら true。失敗（未対応/破損画像など）なら false を返し、error に理由を入れる。
        /// </summary>
        public static bool ApplyUserIcon(string userImagePath, out string error)
        {
            error = "";
            if (string.IsNullOrEmpty(userImagePath) || !File.Exists(userImagePath))
            {
                error = "画像ファイルが見つかりません。";
                return false;
            }
            if (!IconIcoWriter.TryWriteIco(userImagePath, UserIcoPath, out error))
                return false;
            // すべての .lnk を、変換した共通の .ico に向ける。
            UpdateAll(_ => $"{UserIcoPath},0");
            return true;
        }

        /// <summary>error を必要としない呼び出し向けのオーバーロード。</summary>
        public static bool ApplyUserIcon(string userImagePath) => ApplyUserIcon(userImagePath, out _);

        /// <summary>
        /// 既知 .lnk の IconLocation を、その .lnk 自身のターゲット exe（埋め込み app.ico）へ戻す。
        /// 現在のプロセスパス（stub や開発ビルドでは current 配下と異なりうる）ではなく、
        /// 各ショートカットが実際に起動する exe を使うことで、常に正しい既定アイコンへ戻す。
        /// </summary>
        public static void ResetToDefault()
        {
            // 各 .lnk のターゲット exe を読み、その ",0"（埋め込み既定）を指す。
            UpdateAll(sc => { string target = sc.TargetPath; return string.IsNullOrEmpty(target) ? null : $"{target},0"; });
        }

        /// <summary>
        /// 存在する既知 .lnk それぞれについて、iconLocationFor が返す IconLocation を設定する。
        /// iconLocationFor が null を返した .lnk はスキップする。1 つでも更新したら SHChangeNotify する。
        /// </summary>
        private static void UpdateAll(Func<dynamic, string?> iconLocationFor)
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
                    if (SetShortcutIcon(lnk, iconLocationFor)) any = true;
                }
                catch { /* 1 つ失敗しても他を試す */ }
            }
            if (any) SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
        }

        /// <summary>
        /// WScript.Shell 経由で .lnk を開き、iconLocationFor が返す IconLocation を設定して保存する。
        /// 設定した場合 true。iconLocationFor が null を返したら何もせず false。
        /// </summary>
        private static bool SetShortcutIcon(string lnkPath, Func<dynamic, string?> iconLocationFor)
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell")!;
            dynamic shell = Activator.CreateInstance(shellType)!;
            try
            {
                dynamic sc = shell.CreateShortcut(lnkPath);
                try
                {
                    string? iconLocation = iconLocationFor(sc);
                    if (iconLocation == null) return false;
                    sc.IconLocation = iconLocation;
                    sc.Save();
                    return true;
                }
                finally
                {
                    Marshal.FinalReleaseComObject(sc);
                }
            }
            finally
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }
    }
}
