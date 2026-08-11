using System;
using System.Windows;

namespace SCtoolGui
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            try
            {
                var mainWindow = new MainWindow();
                mainWindow.Show();

                // 初回セットアップウィザード（インストール版の初回のみ）。
                // MainWindow を表示した直後に、その前面へ Owner 付きモーダルで出す。
                // Owner を設定することで背後のメインは自動的に無効化（モーダルロック）される。
                ShowSetupWizardIfNeeded(mainWindow);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"起動エラー: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 未完了かつインストール版のときだけ、初回セットアップウィザードをメインの前面に
        /// モーダル表示する。完了時は設定を保存し、選択に従ってショートカットを作成する。
        /// </summary>
        private static void ShowSetupWizardIfNeeded(Window owner)
        {
            try
            {
                var settings = new SettingsManager();
                settings.Load();
                bool isInstalled = new AppUpdateService().IsInstalled;

                if (!SettingsManager.ShouldShowSetupWizard(settings.Current.SetupCompleted, isInstalled))
                    return;

                var wizard = new SetupWizardWindow(
                    settings.Current.SaveDirectory,
                    settings.Current.SaveInWindowNameFolder)
                {
                    Owner = owner,
                };
                wizard.Activate();

                if (wizard.ShowDialog() == true)
                {
                    settings.Current.SaveDirectory = wizard.SelectedSaveDirectory;
                    settings.Current.SaveInWindowNameFolder = wizard.SaveInWindowNameFolder;
                    settings.Current.SetupCompleted = true;
                    settings.Save();

                    var choice = ShortcutLocationResolver.Resolve(
                        wizard.CreateDesktopShortcut, wizard.CreateStartMenuShortcut);
                    ShortcutInstaller.Create(choice, isInstalled);
                }
                // キャンセル/閉じるは SetupCompleted=false のまま（次回再表示）
            }
            catch { }
        }
    }
}
