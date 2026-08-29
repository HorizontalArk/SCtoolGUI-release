using System.Windows;
using Microsoft.Win32;

namespace SCtoolGui
{
    public partial class SetupWizardWindow : Window
    {
        public bool CreateDesktopShortcut => ChkDesktop.IsChecked == true;
        public bool CreateStartMenuShortcut => ChkStartMenu.IsChecked == true;
        public string SelectedSaveDirectory { get; private set; }
        public bool SaveInWindowNameFolder => ChkPerAppFolder.IsChecked == true;

        public SetupWizardWindow(string defaultSaveDirectory, bool defaultSaveInWindowNameFolder)
        {
            InitializeComponent();
            SelectedSaveDirectory = defaultSaveDirectory;
            TxtSaveDir.Text = defaultSaveDirectory;
            ChkPerAppFolder.IsChecked = defaultSaveInWindowNameFolder;
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFolderDialog { Title = "画像の保存先を選択" };
            if (!string.IsNullOrEmpty(SelectedSaveDirectory))
                dlg.InitialDirectory = SelectedSaveDirectory;
            if (dlg.ShowDialog() == true)
            {
                SelectedSaveDirectory = dlg.FolderName;
                TxtSaveDir.Text = dlg.FolderName;
            }
        }

        private void BtnDone_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
