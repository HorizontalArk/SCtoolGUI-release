using System.Collections.Generic;
using System.Windows;

namespace SCtoolGui
{
    public partial class SettingsWindow : Window
    {
        public string ResultSaveDir { get; private set; } = "";
        public uint ResultModifiers { get; private set; }
        public uint ResultKey { get; private set; }
        public bool ResultAppTopmost { get; private set; }
        public bool ResultSaveInWindowNameFolder { get; private set; }
        public bool ResultResetSettingsOnWindowChange { get; private set; }
        public bool ResultAutoCopyClipboard { get; private set; }
        public bool ResultPlayShutterSound { get; private set; }
        
        // ★追加: 音量の結果を受け渡すプロパティ
        public double ResultShutterVolume { get; private set; }

        public bool ResultAlwaysRunAsAdmin { get; private set; }

        public string ResultTheme { get; private set; } = "System";

        public SettingsWindow(string saveDir, uint modifiers, uint key, bool appTopmost, bool saveInWindowFolder, bool resetSettings, bool autoCopy, bool playShutterSound, double shutterVolume, bool alwaysRunAsAdmin, string theme)
        {
            InitializeComponent();
            TxtSaveDir.Text = saveDir;
            ChkCtrl.IsChecked = (modifiers & 0x0002) != 0;
            ChkShift.IsChecked = (modifiers & 0x0004) != 0;
            ChkAlt.IsChecked = (modifiers & 0x0001) != 0;

            var keys = new List<string> { "S", "A", "Z", "X", "C", "V", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12" };
            CmbKey.ItemsSource = keys;
            
            string currentKeyStr = (key >= 0x70 && key <= 0x7B) ? $"F{key - 0x70 + 1}" : ((char)key).ToString();
            CmbKey.SelectedItem = currentKeyStr;

            ChkAppTopmost.IsChecked = appTopmost;
            ChkSaveInWindowFolder.IsChecked = saveInWindowFolder;
            ChkResetSettingsOnWindowChange.IsChecked = resetSettings;
            ChkAutoCopyClipboard.IsChecked = autoCopy;
            
            ChkPlayShutterSound.IsChecked = playShutterSound;
            
            // ★追加: 0.0〜1.0 の音量を 0〜100 に変換してスライダーにセット
            SldVolume.Value = shutterVolume * 100;

            ChkAlwaysRunAsAdmin.IsChecked = alwaysRunAsAdmin;

            CmbTheme.SelectedIndex = theme switch { "Light" => 1, "Dark" => 2, _ => 0 };
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "保存フォルダを選択してください",
            };

            // 存在しないパスを渡すとダイアログが既定の場所で開いてしまうため、
            // 実在する場合だけ初期位置として指定する
            if (System.IO.Directory.Exists(TxtSaveDir.Text))
            {
                dialog.InitialDirectory = TxtSaveDir.Text;
            }

            if (dialog.ShowDialog() == true)
            {
                TxtSaveDir.Text = dialog.FolderName;
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            ResultSaveDir = TxtSaveDir.Text;
            ResultModifiers = 0;
            if (ChkCtrl.IsChecked == true) ResultModifiers |= 0x0002;
            if (ChkShift.IsChecked == true) ResultModifiers |= 0x0004;
            if (ChkAlt.IsChecked == true) ResultModifiers |= 0x0001;

            string selectedKey = CmbKey.SelectedItem?.ToString() ?? "S";
            if (selectedKey.StartsWith("F")) {
                ResultKey = (uint)(0x70 + int.Parse(selectedKey.Substring(1)) - 1);
            } else {
                ResultKey = (uint)selectedKey[0];
            }

            ResultAppTopmost = ChkAppTopmost.IsChecked == true;
            ResultSaveInWindowNameFolder = ChkSaveInWindowFolder.IsChecked == true;
            ResultResetSettingsOnWindowChange = ChkResetSettingsOnWindowChange.IsChecked == true;
            ResultAutoCopyClipboard = ChkAutoCopyClipboard.IsChecked == true;
            ResultPlayShutterSound = ChkPlayShutterSound.IsChecked == true;
            
            // ★追加: スライダーの 0〜100 を 0.0〜1.0 に戻して保存
            ResultShutterVolume = SldVolume.Value / 100.0;

            ResultAlwaysRunAsAdmin = ChkAlwaysRunAsAdmin.IsChecked == true;

            ResultTheme = CmbTheme.SelectedIndex switch { 1 => "Light", 2 => "Dark", _ => "System" };

            this.DialogResult = true;
        }
    }
}