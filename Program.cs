using System;
using Velopack;

namespace SCtoolGui
{
    /// <summary>
    /// アプリのエントリポイント。
    ///
    /// Velopack のインストール/更新フックは WPF が立ち上がる前に処理する必要があるため、
    /// 最初に <see cref="VelopackApp"/> を Run する（asInvoker なので UAC は出ない）。
    ///
    /// 管理者権限は常時は要求しない。管理者権限で動く対象ウィンドウを選んだ時にだけ、
    /// <see cref="ProcessElevation"/> 経由で昇格して起動し直す（普段は UAC を出さないため）。
    /// </summary>
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            VelopackApp.Build().Run();

            // 「常に管理者として起動」が有効なら、非管理者時に昇格して起動し直す。
            // 設定が読めなくても通常起動は続ける。
            try
            {
                var settings = new SettingsManager();
                settings.Load();
                if (settings.Current.AlwaysRunAsAdmin && !ProcessElevation.IsCurrentProcessElevated())
                {
                    if (ProcessElevation.RelaunchAsAdmin()) return;
                }
            }
            catch { }

            var app = new App();
            app.InitializeComponent();
            app.Run();
        }
    }
}
