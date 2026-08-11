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
                // 初回セットアップウィザードは、MainWindow の表示完了後（Loaded）に
                // その前面へモーダル表示する（MainWindow.ShowSetupWizardIfNeeded）。
                // 設定は MainWindow 自身の SettingsManager を使うため、保存内容が同セッションに反映される。
                var mainWindow = new MainWindow();
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"起動エラー: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
