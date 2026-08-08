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
            }
            catch (Exception ex)
            {
                MessageBox.Show($"起動エラー: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}