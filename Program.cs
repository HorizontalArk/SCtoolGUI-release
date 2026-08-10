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
            // 単一起動ガード(Mutex)より前に行うことで、昇格再起動時の取り合いを避ける。
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

            // 多重起動を防ぐ。既に起動していれば、そのウィンドウを前面に出して終了する。
            if (!SingleInstance.TryAcquire())
            {
                SingleInstance.ActivateExisting();
                return;
            }

            var app = new App();
            app.InitializeComponent();

            // 初回セットアップウィザード（インストール版の初回のみ）。
            // App.InitializeComponent 後に出すことで、テーマ(Fluent)リソースが解決される。
            try
            {
                var setupSettings = new SettingsManager();
                setupSettings.Load();
                bool isInstalled = new AppUpdateService().IsInstalled;

                if (SettingsManager.ShouldShowSetupWizard(setupSettings.Current.SetupCompleted, isInstalled))
                {
                    var wizard = new SetupWizardWindow(
                        setupSettings.Current.SaveDirectory,
                        setupSettings.Current.SaveInWindowNameFolder);

                    if (wizard.ShowDialog() == true)
                    {
                        setupSettings.Current.SaveDirectory = wizard.SelectedSaveDirectory;
                        setupSettings.Current.SaveInWindowNameFolder = wizard.SaveInWindowNameFolder;
                        setupSettings.Current.SetupCompleted = true;
                        setupSettings.Save();

                        var choice = ShortcutLocationResolver.Resolve(
                            wizard.CreateDesktopShortcut, wizard.CreateStartMenuShortcut);
                        ShortcutInstaller.Create(choice, isInstalled);
                    }
                    // キャンセル/閉じるは SetupCompleted=false のまま（次回再表示）
                }
            }
            catch { }

            app.Run();
        }
    }
}
