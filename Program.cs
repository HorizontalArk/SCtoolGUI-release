using System;
using System.Diagnostics;
using System.Security.Principal;
using Velopack;

namespace SCtoolGui
{
    /// <summary>
    /// アプリのエントリポイント。
    ///
    /// Velopack のインストール/更新フックは WPF が立ち上がる前に処理する必要があるため、
    /// 最初に <see cref="VelopackApp"/> を Run する。フック実行時はここで完結して即終了するので、
    /// その後の自己昇格には到達しない（＝インストール/更新中に UAC は出ない）。
    ///
    /// 通常起動時は、管理者権限のウィンドウに対する最前面固定などのために、
    /// 管理者でなければ自分自身を昇格して起動し直す。
    /// </summary>
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            VelopackApp.Build().Run();

            if (!IsAdministrator())
            {
                // 昇格インスタンスを起動できたら、このインスタンスは役目を終える。
                if (TryRelaunchAsAdmin(args)) return;

                // ユーザーが UAC をキャンセルした場合などは、通常権限のまま起動する。
                // （撮影は可能。管理者ウィンドウの最前面固定のみ効かない。）
            }

            var app = new App();
            app.InitializeComponent();
            app.Run();
        }

        private static bool IsAdministrator()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static bool TryRelaunchAsAdmin(string[] args)
        {
            try
            {
                string? exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath)) return false;

                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true, // runas には ShellExecute が必要
                    Verb = "runas",
                    Arguments = string.Join(" ", args),
                };
                Process.Start(psi);
                return true;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // UAC キャンセル(1223)など。昇格せずに続行する。
                return false;
            }
        }
    }
}
