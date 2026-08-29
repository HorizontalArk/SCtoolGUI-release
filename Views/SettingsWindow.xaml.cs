using System.Collections.Generic;
using System.Windows;

namespace SCtoolGui
{
    /// <summary>bool を反転する。統一チェック ON のときプレビュー個別項目を無効化するために使う。</summary>
    public class InverseBoolConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => value is bool b ? !b : true;
        public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => value is bool b ? !b : false;
    }

    public partial class SettingsWindow : Window
    {
        // ダイアログ表示元から渡された設定そのもの。保存ボタン押下時にこの参照へ直接書き戻すため、
        // 呼び出し側は ShowDialog() が true を返した後、追加のコピー作業なしに最新値を参照できる。
        private readonly AppSettings _settings;

        public SettingsWindow(AppSettings settings, bool developerUnlocked)
        {
            _settings = settings;
            InitializeComponent();
            TxtSaveDir.Text = settings.SaveDirectory;
            ChkCtrl.IsChecked = (settings.HotkeyModifiers & 0x0002) != 0;
            ChkShift.IsChecked = (settings.HotkeyModifiers & 0x0004) != 0;
            ChkAlt.IsChecked = (settings.HotkeyModifiers & 0x0001) != 0;

            // A〜Z ＋ 0〜9 ＋ F1〜F12 を選択肢にする
            var keys = new List<string>();
            for (char c = 'A'; c <= 'Z'; c++) keys.Add(c.ToString());
            for (char c = '0'; c <= '9'; c++) keys.Add(c.ToString());
            for (int i = 1; i <= 12; i++) keys.Add($"F{i}");
            CmbKey.ItemsSource = keys;

            uint key = settings.HotkeyKey;
            string currentKeyStr = (key >= 0x70 && key <= 0x7B) ? $"F{key - 0x70 + 1}" : ((char)key).ToString();
            CmbKey.SelectedItem = currentKeyStr;

            ChkAppTopmost.IsChecked = settings.AppTopmost;
            ChkSaveInWindowFolder.IsChecked = settings.SaveInWindowNameFolder;
            ChkResetSettingsOnWindowChange.IsChecked = settings.ResetSettingsOnWindowChange;
            ChkAutoCopyClipboard.IsChecked = settings.AutoCopyClipboard;

            ChkPlayShutterSound.IsChecked = settings.PlayShutterSound;

            // 0.0〜1.0 の音量を 0〜100 に変換してスライダーにセット
            SldVolume.Value = settings.ShutterVolume * 100;

            ChkAlwaysRunAsAdmin.IsChecked = settings.AlwaysRunAsAdmin;

            CmbTheme.SelectedIndex = settings.Theme switch { "Light" => 1, "Dark" => 2, _ => 0 };

            TxtIconPath.Text = settings.IconPath;

            CmbVerticalSide.SelectedIndex = settings.VerticalPreviewSide == "Left" ? 1 : 0;
            CmbAutoSwitch.SelectedIndex = settings.PreviewAutoSwitch switch
            {
                "Off" => 0,
                "Force" => 2,
                _ => 1, // Prompt
            };

            ChkUseWindowTitleForFileName.IsChecked = settings.UseWindowTitleForFileName;
            CmbCopySource.SelectedIndex = settings.CopySource switch
            {
                "TempPreview" => 1,
                "FreshPreview" => 2,
                _ => 0,
            };

            ChkUnifyFocus.IsChecked = settings.UnifyCaptureAndPreviewFocus;
            ChkRestoreFocusOnCapture.IsChecked = settings.RestoreFocusToToolOnCapture;
            ChkRestoreFocusOnPreview.IsChecked = settings.RestoreFocusToToolOnPreview;

            // マーカー解錠時のみ開発者モードトグルを出す。未解錠なら一切出さない。
            ChkDeveloperMode.Visibility = developerUnlocked ? Visibility.Visible : Visibility.Collapsed;
            ChkDeveloperMode.IsChecked = developerUnlocked && settings.DeveloperModeEnabled;
            ChkIncludePrereleases.IsChecked = settings.IncludePrereleases;
            UpdateDeveloperTabVisibility();
            UpdateDevStatus();
        }

        private void ChkDeveloperMode_Changed(object sender, RoutedEventArgs e)
            => UpdateDeveloperTabVisibility();

        /// <summary>開発者タブは、トグルが表示済み(=解錠)かつ ON のときだけ表示する。判定自体はDeveloperModeGateへ委譲。</summary>
        private void UpdateDeveloperTabVisibility()
        {
            bool unlocked = ChkDeveloperMode.Visibility == Visibility.Visible;
            bool toggleOn = ChkDeveloperMode.IsChecked == true;
            bool show = DeveloperModeGate.ShouldShowDeveloperTab(unlocked, toggleOn);
            if (TabDeveloper != null)
                TabDeveloper.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        private string? _devStatusText;
        private void UpdateDevStatus()
            => TxtDevStatus.Text = _devStatusText ?? "（情報なし）";

        /// <summary>MainWindow から現在バージョン等の状態文言を渡す。</summary>
        public void SetDeveloperStatus(string text) { _devStatusText = text; UpdateDevStatus(); }

        // バージョン一覧・適用は MainWindow 側のサービスに委譲するため、コールバックで橋渡しする。
        public System.Func<System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<string>>>? LoadVersions;
        public System.Func<string, System.Threading.Tasks.Task>? ApplyVersion;

        private async void BtnReloadVersions_Click(object sender, RoutedEventArgs e)
        {
            if (LoadVersions == null) return;
            CmbVersions.ItemsSource = null;
            var list = await LoadVersions();
            CmbVersions.ItemsSource = list;
            if (list.Count > 0) CmbVersions.SelectedIndex = 0;
        }

        private async void BtnApplyVersion_Click(object sender, RoutedEventArgs e)
        {
            if (ApplyVersion == null || CmbVersions.SelectedItem is not string ver) return;
            var ok = MessageBox.Show($"バージョン {ver} へ変更して再起動します。よろしいですか？",
                "バージョン変更", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (ok != MessageBoxResult.Yes) return;
            await ApplyVersion(ver);
        }

        private void BtnBrowseIcon_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "アイコン画像を選択してください",
                Filter = "画像ファイル (*.ico;*.png;*.jpg;*.jpeg;*.bmp)|*.ico;*.png;*.jpg;*.jpeg;*.bmp|すべてのファイル (*.*)|*.*",
            };
            if (dialog.ShowDialog() == true)
            {
                TxtIconPath.Text = dialog.FileName;
            }
        }

        private void BtnClearIcon_Click(object sender, RoutedEventArgs e)
        {
            TxtIconPath.Text = "";
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
            _settings.SaveDirectory = TxtSaveDir.Text;

            uint modifiers = 0;
            if (ChkCtrl.IsChecked == true) modifiers |= 0x0002;
            if (ChkShift.IsChecked == true) modifiers |= 0x0004;
            if (ChkAlt.IsChecked == true) modifiers |= 0x0001;
            _settings.HotkeyModifiers = modifiers;

            string selectedKey = CmbKey.SelectedItem?.ToString() ?? "S";
            // "F1"〜"F12" はファンクションキー、"F" 単体は通常のキーとして扱う
            _settings.HotkeyKey = (selectedKey.Length > 1 && selectedKey[0] == 'F')
                ? (uint)(0x70 + int.Parse(selectedKey.Substring(1)) - 1)
                : (uint)selectedKey[0];

            _settings.AppTopmost = ChkAppTopmost.IsChecked == true;
            _settings.SaveInWindowNameFolder = ChkSaveInWindowFolder.IsChecked == true;
            _settings.ResetSettingsOnWindowChange = ChkResetSettingsOnWindowChange.IsChecked == true;
            _settings.AutoCopyClipboard = ChkAutoCopyClipboard.IsChecked == true;
            _settings.PlayShutterSound = ChkPlayShutterSound.IsChecked == true;

            // スライダーの 0〜100 を 0.0〜1.0 に戻して保存
            _settings.ShutterVolume = SldVolume.Value / 100.0;

            _settings.AlwaysRunAsAdmin = ChkAlwaysRunAsAdmin.IsChecked == true;

            _settings.Theme = CmbTheme.SelectedIndex switch { 1 => "Light", 2 => "Dark", _ => "System" };

            _settings.IconPath = TxtIconPath.Text;

            _settings.VerticalPreviewSide = CmbVerticalSide.SelectedIndex == 1 ? "Left" : "Right";
            _settings.PreviewAutoSwitch = CmbAutoSwitch.SelectedIndex switch
            {
                0 => "Off",
                2 => "Force",
                _ => "Prompt",
            };

            _settings.UseWindowTitleForFileName = ChkUseWindowTitleForFileName.IsChecked == true;
            _settings.CopySource = CmbCopySource.SelectedIndex switch
            {
                1 => "TempPreview",
                2 => "FreshPreview",
                _ => "LastSaved",
            };

            _settings.UnifyCaptureAndPreviewFocus = ChkUnifyFocus.IsChecked == true;
            _settings.RestoreFocusToToolOnCapture = ChkRestoreFocusOnCapture.IsChecked == true;
            _settings.RestoreFocusToToolOnPreview = ChkRestoreFocusOnPreview.IsChecked == true;

            _settings.DeveloperModeEnabled = ChkDeveloperMode.IsChecked == true;
            _settings.IncludePrereleases = ChkIncludePrereleases.IsChecked == true;

            this.DialogResult = true;
        }
    }
}