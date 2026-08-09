using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.IO;

namespace SCtoolGui
{
    public partial class MainWindow
    {
        /// <summary>
        /// 現在選択中のターゲット。未選択なら null。
        /// ウィンドウタイトルではなく実行ファイルパスで識別されるため、
        /// アプリ側でタイトルが変化しても対象を見失わない。
        /// </summary>
        private TargetInfo? CurrentTarget =>
            _settingsManager.Current.Targets
                .FirstOrDefault(t => t.Key == _settingsManager.Current.LastSelectedTargetKey);

        /// <summary>直近にターゲットを捕捉できたウィンドウハンドル。タイトル変更を跨いで追跡するために保持する。</summary>
        private IntPtr _trackedHandle = IntPtr.Zero;

        /// <summary>
        /// 選択中のターゲットに対応する、現在起動中のウィンドウを探す。無ければ null。
        /// ハンドル → exeパス → タイトル の順に照合する（WindowMatcher 参照）。
        /// </summary>
        private WindowItem? FindTargetWindow()
        {
            var target = CurrentTarget;
            if (target == null) return null;

            var windows = WindowManager.GetTargetWindows();
            var result = WindowMatcher.Match(
                target,
                _trackedHandle,
                windows.Cast<IWindowSnapshot>().ToList(),
                WindowManager.IsWindowAlive);

            if (result.Window is not WindowItem found)
            {
                _trackedHandle = IntPtr.Zero;
                return null;
            }

            // 次回以降の照合を安定させるため、捕捉できたハンドルと最新タイトルを記録する
            _trackedHandle = found.Handle;
            target.LastKnownTitle = found.Title;

            if (string.IsNullOrEmpty(target.ExecutablePath) && !string.IsNullOrEmpty(found.ExecutablePath))
            {
                // 旧データや取得失敗分に、exeパスを後から補完する
                target.ExecutablePath = found.ExecutablePath;
            }

            return found;
        }

        private void InitializeWindowList()
        {
            RefreshWindowList();
            _statusTimer.Interval = TimeSpan.FromSeconds(2);
            _statusTimer.Tick += (s, e) => UpdateWindowStatus();
            _statusTimer.Start();

            // 起動時にも、初期選択されているウィンドウの一時プレビューを撮影して表示する
            CaptureTempPreview(verbose: false);
        }

        private void CmbWindows_DropDownOpened(object sender, EventArgs e)
        {
            RefreshWindowList();
            Log("ウィンドウ一覧を更新しました。");
        }

        /// <summary>
        /// 管理者権限で動く対象を選んだ時に、昇格するか確認する。
        /// 「はい」で管理者として再起動（この後アプリは終了）、「いいえ」で選択を元の対象に戻す。
        /// 戻り値 true = そのまま通常権限で選択を続行してよい / false = 中断（呼び出し側は return する）。
        /// </summary>
        private bool ConfirmAndElevateForTarget(WindowItem selected, SelectionChangedEventArgs e)
        {
            var answer = MessageBox.Show(
                $"[{selected.Title}] は管理者権限で動作しています。\n" +
                "最前面固定などを使うには、SCtool を管理者として再起動する必要があります。\n\n" +
                "管理者として再起動しますか？",
                "管理者権限が必要です",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (answer == MessageBoxResult.Yes)
            {
                // 昇格後にも同じ対象を復元できるよう、先に登録・保存してから再起動する。
                var t = TargetRegistry.GetOrAdd(
                    _settingsManager.Current.Targets, selected.ExecutablePath, selected.Title);
                _settingsManager.Current.LastSelectedTargetKey = t.Key;
                _settingsManager.Current.LastSelectedWindow = selected.Title;
                _settingsManager.Save();

                if (ProcessElevation.RelaunchAsAdmin())
                {
                    Application.Current.Shutdown();
                    return false;
                }

                // 昇格をキャンセルされた → 通常権限のまま選択を続行する
                Log("管理者としての再起動をキャンセルしました。通常権限のまま続行します。");
                return true;
            }

            // いいえ → 選択を元の対象に戻す（再帰的なイベント発火を避けるため一時的に購読解除）
            CmbWindows.SelectionChanged -= CmbWindows_SelectionChanged;
            try
            {
                CmbWindows.SelectedItem = e.RemovedItems.Count > 0 ? e.RemovedItems[0] : null;
            }
            finally
            {
                CmbWindows.SelectionChanged += CmbWindows_SelectionChanged;
            }
            return false;
        }

        private void CmbWindows_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.RemovedItems.Count > 0 && e.RemovedItems[0] is WindowItem oldItem && oldItem.Handle != IntPtr.Zero)
            {
                WindowManager.SetAlwaysOnTop(oldItem.Handle, false);
            }

            if (CmbWindows.SelectedItem is WindowItem selected) {
                // 管理者権限で動く対象は、非管理者のままだと最前面固定などが効かない。
                // 選択の時点で判定し、昇格するか確認する（撮影自体は非管理者でも可能）。
                if (!ProcessElevation.IsCurrentProcessElevated()
                    && ProcessElevation.IsWindowProcessElevated(selected.Handle))
                {
                    if (!ConfirmAndElevateForTarget(selected, e)) return;
                }

                // タイトルではなく実行ファイルパスでターゲットを登録・特定する
                var target = TargetRegistry.GetOrAdd(
                    _settingsManager.Current.Targets, selected.ExecutablePath, selected.Title);

                _settingsManager.Current.LastSelectedTargetKey = target.Key;
                _settingsManager.Current.LastSelectedWindow = selected.Title; // 旧バージョン互換
                _trackedHandle = selected.Handle;

                SaveAndLog($"ウィンドウを [{target.DisplayName}] に切り替えました。");

                ChkAlwaysOnTop.IsChecked = false;

                if (_settingsManager.Current.ResetSettingsOnWindowChange)
                {
                    ChkCutTab.IsChecked = false;
                }
                else if (target.TopCut > 0)
                {
                    ChkCutTab.IsChecked = true;
                    TxtTopCut.Text = target.TopCut.ToString();
                    Log($"[{target.DisplayName}] のカット設定({target.TopCut}px)を自動復元しました。");
                }
                else
                {
                    ChkCutTab.IsChecked = false;
                }

                CaptureTempPreview(verbose: false);
            }
            UpdateWindowStatus();
        }

        private void RefreshWindowList()
        {
            CmbWindows.SelectionChanged -= CmbWindows_SelectionChanged;

            try
            {
                var activeWindows = WindowManager.GetTargetWindows();
                var target = CurrentTarget;

                // 選択中ターゲットに対応するウィンドウを、タイトル変更を跨いで特定する
                var matched = target == null
                    ? null
                    : WindowMatcher.Match(
                        target,
                        _trackedHandle,
                        activeWindows.Cast<IWindowSnapshot>().ToList(),
                        WindowManager.IsWindowAlive).Window as WindowItem;

                if (matched != null)
                {
                    _trackedHandle = matched.Handle;
                }
                else if (target != null)
                {
                    // 未起動でも一覧に残し、選択状態を維持できるようにする
                    activeWindows.Insert(0, new WindowItem
                    {
                        Title = target.DisplayName,
                        Handle = IntPtr.Zero,
                        ExecutablePath = target.ExecutablePath,
                    });
                    matched = activeWindows[0];
                    _trackedHandle = IntPtr.Zero;
                }

                CmbWindows.ItemsSource = activeWindows;
                CmbWindows.SelectedItem = matched ?? CmbWindows.Items.Cast<WindowItem>().FirstOrDefault();

                UpdateWindowStatus();
            }
            finally 
            { 
                CmbWindows.SelectionChanged += CmbWindows_SelectionChanged;
            }
        }

        private void UpdateWindowStatus()
        {
            var target = CurrentTarget;
            if (target == null) {
                TxtWindowStatus.Text = "対象アプリが選択されていません";
                TxtWindowStatus.Foreground = Brushes.Gray;
                return;
            }

            string name = target.DisplayName;
            var targetWindow = FindTargetWindow();

            if (targetWindow != null) {
                if (CmbWindows.SelectedItem is WindowItem selected)
                {
                    selected.Handle = targetWindow.Handle;
                }

                if (WindowManager.IsWindowMinimized(targetWindow.Handle)) {
                    TxtWindowStatus.Text = $"{name} は最小化されています";
                    TxtWindowStatus.Foreground = Brushes.DarkOrange;
                } else {
                    TxtWindowStatus.Text = $"{name} は起動中です";
                    TxtWindowStatus.Foreground = Brushes.Green;

                    if (ChkAlwaysOnTop.IsChecked == true)
                    {
                        WindowManager.SetAlwaysOnTop(targetWindow.Handle, true);
                    }
                }
            } else {
                if (CmbWindows.SelectedItem is WindowItem selected)
                {
                    selected.Handle = IntPtr.Zero;
                }
                TxtWindowStatus.Text = $"{name} が起動されていません";
                TxtWindowStatus.Foreground = Brushes.Red;

                if (ChkAlwaysOnTop.IsChecked == true)
                {
                    ChkAlwaysOnTop.IsChecked = false;
                    Log($"{name}の最前面固定を解除しました");
                }
            }

            UpdateCurrentSavePathDisplay();
        }

        private void ChkAlwaysOnTop_Click(object sender, RoutedEventArgs e)
        {
            string targetTitle = CurrentTarget?.DisplayName ?? "";

            if (ChkAlwaysOnTop.IsChecked == true)
            {
                var targetWindow = FindTargetWindow();

                if (targetWindow == null)
                {
                    Log($"{targetTitle}は非起動中であるため、操作がキャンセルされました");
                    ChkAlwaysOnTop.IsChecked = false;
                    return;
                }

                if (WindowManager.IsWindowMinimized(targetWindow.Handle))
                {
                    Log($"{targetTitle}は最小化中であるため、操作がキャンセルされました");
                    ChkAlwaysOnTop.IsChecked = false;
                    return;
                }

                if (CmbWindows.SelectedItem is WindowItem selected)
                {
                    selected.Handle = targetWindow.Handle;
                }

                bool success = WindowManager.SetAlwaysOnTop(targetWindow.Handle, true);

                if (success)
                {
                    Log($"{targetTitle}を最前面に固定しました");
                }
                else
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    Log($"【拒否】{targetTitle}の最前面化をOSにブロックされました(Error:{errorCode})。対象アプリの権限が強いか、仕様により操作できません。SCtoolを管理者として実行すると解決する場合があります。");
                    ChkAlwaysOnTop.IsChecked = false;
                }
            }
            else
            {
                IntPtr handleToUnset = FindTargetWindow()?.Handle ?? IntPtr.Zero;

                if (handleToUnset != IntPtr.Zero)
                {
                    WindowManager.SetAlwaysOnTop(handleToUnset, false);
                }
                Log($"{targetTitle}の最前面固定を解除しました");
            }
        }

        private void UpdateCurrentSavePathDisplay()
        {
            string baseDir = _settingsManager.Current.SaveDirectory;

            string displayPath = baseDir;
            if (_settingsManager.Current.SaveInWindowNameFolder)
            {
                // タイトルではなく表示名を使うため、アプリ側でタイトルが変わっても保存先は一定に保たれる
                displayPath = System.IO.Path.Combine(baseDir, CurrentFolderName);
            }

            if (TxtCurrentSavePath != null)
            {
                TxtCurrentSavePath.Text = $"現在の保存先: {displayPath}";
            }
        }

        private void BtnRenameTarget_Click(object sender, RoutedEventArgs e)
        {
            var target = CurrentTarget;
            if (target == null)
            {
                Log("【警告】対象アプリが選択されていません。");
                return;
            }

            string defaultName = TargetInfo.DeriveDisplayName(target.ExecutablePath, target.LastKnownTitle);
            var dialog = new RenameWindow(target.DisplayName, defaultName) { Owner = this };

            if (dialog.ShowDialog() != true) return;

            string oldName = target.DisplayName;
            if (dialog.ResultName == oldName) return;

            target.DisplayName = dialog.ResultName;
            SaveAndLog($"呼び名を [{oldName}] から [{target.DisplayName}] に変更しました。");

            UpdateCurrentSavePathDisplay();
            UpdateWindowStatus();
        }

        /// <summary>
        /// 選択中ターゲットの保存フォルダ名。表示名を元にするため、
        /// アプリのタイトルが変化しても同じフォルダに保存され続ける。
        /// </summary>
        private string CurrentFolderName
        {
            get
            {
                string name = CurrentTarget?.DisplayName ?? "";
                return string.IsNullOrEmpty(name) ? "{ウィンドウ名}" : GetSafeFileName(name);
            }
        }

        private string GetSafeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Unknown";
            string invalidChars = new string(System.IO.Path.GetInvalidFileNameChars()) + new string(System.IO.Path.GetInvalidPathChars());
            foreach (char c in invalidChars)
            {
                name = name.Replace(c.ToString(), "_");
            }
            return name;
        }
    }
}