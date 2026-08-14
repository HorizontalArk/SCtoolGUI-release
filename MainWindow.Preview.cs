using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace SCtoolGui
{
    public partial class MainWindow
    {
        private bool _isTempPreviewMode = false;
        private bool _isFinalOutputMode = false;
        private bool _isDraggingCutLine = false;

        /// <summary>最後に ShowPreview した画像の向き（自動切替の判定材料）。</summary>
        private PreviewMode? _lastShownImageOrientation;

        private static readonly string TempPreviewPath = Path.Combine(Path.GetTempPath(), "SCtool_temp_preview.jpg");

        /// <summary>最後に保存した画像が実際にディスク上に存在するか。</summary>
        private bool HasLastCapture => !string.IsNullOrEmpty(_lastCapturedPath) && File.Exists(_lastCapturedPath);

        /// <summary>現在のカット量。カットOFF時や数値が不正な場合は 0。</summary>
        private int CurrentCutValue =>
            (ChkCutTab?.IsChecked == true && int.TryParse(TxtTopCut?.Text, out int val)) ? Math.Max(0, val) : 0;

        /// <summary>選択中のウィンドウを一時プレビューとして撮影し、成功したら表示する。</summary>
        /// <param name="verbose">true の場合、成功／失敗をログに出力する。</param>
        private bool CaptureTempPreview(bool verbose)
        {
            if (CmbWindows.SelectedItem is not WindowItem selected) return false;

            if (selected.Handle == IntPtr.Zero)
            {
                if (verbose) Log("【警告】ウィンドウが選択されていないか、起動していません。");
                return false;
            }
            if (WindowManager.IsWindowMinimized(selected.Handle))
            {
                if (verbose) Log("【警告】選択中のウィンドウは最小化されているためプレビューできません。");
                return false;
            }

            try
            {
                if (!ScreenCapture.SavePreviewOnly(selected.Handle, TempPreviewPath))
                {
                    // 対象を前面にできなかった場合もここに来る（別画面の写り込みを防ぐため中止している）
                    if (verbose) Log("【エラー】プレビューを取得できませんでした。対象を前面に表示できていない可能性があります。");
                    return false;
                }

                ShowPreview(TempPreviewPath, isTempPreview: true);
                if (verbose) Log("一時プレビューを更新しました。");
                return true;
            }
            catch (Exception ex)
            {
                if (verbose) Log($"【エラー】プレビュー更新中にエラーが発生しました: {ex.Message}");
                return false;
            }
            finally
            {
                // プレビュー取得のため対象を前面化した可能性があるので、成否に関わらずツールを前面へ戻す。
                // （対象が最小化などでプレビューに失敗しても、ツールが隠れたままにならないようにする）
                BringToolToForeground();
            }
        }

        private void UpdateTempPreview() => CaptureTempPreview(verbose: true);

        /// <summary>カット境界線とオーバーレイの表示を現在の状態に合わせて更新する。</summary>
        private void UpdateCutOverlay(bool forceHide = false)
        {
            if (CutOverlayRect == null || CutBoundaryLine == null) return;

            bool show = !forceHide && ChkCutTab?.IsChecked == true && !_isFinalOutputMode;

            if (show)
            {
                int h = CurrentCutValue;
                CutOverlayRect.Height = h;
                CutOverlayRect.Visibility = Visibility.Visible;
                CutBoundaryLine.Y1 = h;
                CutBoundaryLine.Y2 = h;
                CutBoundaryLine.Visibility = Visibility.Visible;
            }
            else
            {
                CutOverlayRect.Visibility = Visibility.Collapsed;
                CutBoundaryLine.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>保存画像に対する操作ボタン（開く／コピー／削除）の見た目とカーソルを切り替える。</summary>
        private void SetActionButtonsState(bool enabled)
        {
            double opacity = enabled ? 1.0 : 0.4;
            Cursor cursor = enabled ? Cursors.Arrow : Cursors.No;

            foreach (var btn in new[] { BtnOpenFile, BtnCopyClipboard, BtnCopyDropdown, BtnDeleteFile })
            {
                if (btn == null) continue;
                btn.Opacity = opacity;
                btn.Cursor = cursor;
            }

            PreviewContentGrid.Cursor = enabled ? Cursors.Hand : Cursors.Arrow;
        }

        private void UpdateToolTips()
        {
            if (ImgPreview == null) return;

            const string NoImageMessage = "まだ画像が保存されていません";
            bool hasLastCapture = HasLastCapture;

            // 一時プレビューかどうかに関わらず、保存された画像があるかどうかで案内を変える
            string imgTooltip = hasLastCapture
                ? "ダブルクリックで最後に保存した画像を開きます"
                : NoImageMessage;

            if (ChkCutTab.IsChecked == true && !_isFinalOutputMode)
            {
                imgTooltip += "\n\n【カット位置の調整】\n・ホイール回転で上下\n・境界線をドラッグ＆ドロップ\n・Shift＋クリックで指定位置にワープ";
            }

            ImgPreview.ToolTip = imgTooltip;

            BtnOpenFile.ToolTip = hasLastCapture ? "最後に保存した画像を既定のアプリで開きます" : NoImageMessage;
            BtnCopyClipboard.ToolTip = hasLastCapture ? "最後に保存した画像をクリップボードにコピーします" : NoImageMessage;
            BtnDeleteFile.ToolTip = hasLastCapture ? "最後に保存した画像をPCから完全に削除します" : NoImageMessage;
        }

        /// <summary>プレビュー画像を差し替え、カット表示とボタン状態を現在の状態に合わせる。</summary>
        private void ShowPreview(string filePath, bool isTempPreview = false, bool isFinalOutput = false)
        {
            TxtDeleteMessage.Visibility = Visibility.Collapsed;

            ImgPreview.Source = LoadBitmap(filePath);

            if (ImgPreview.Source is BitmapSource bmp)
            {
                _lastShownImageOrientation =
                    PreviewOrientationLogic.DetectImageOrientation(bmp.PixelWidth, bmp.PixelHeight);
            }

            _isTempPreviewMode = isTempPreview;
            _isFinalOutputMode = isFinalOutput;

            UpdateCutOverlay();

            BtnOpenFile.IsEnabled = true;
            BtnCopyClipboard.IsEnabled = true;
            BtnCopyDropdown.IsEnabled = true;
            BtnDeleteFile.IsEnabled = true;

            // 一時プレビュー中かではなく、画像が存在するかどうかでボタンの見た目を変える
            SetActionButtonsState(HasLastCapture);

            UpdateToolTips();

            if (PreviewWatermarkBorder != null)
            {
                PreviewWatermarkBorder.Visibility = isTempPreview ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        /// <summary>ファイルを掴んだままにしないよう、メモリ上に読み切った BitmapImage を返す。</summary>
        private static BitmapImage LoadBitmap(string filePath)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bitmap.UriSource = new Uri(filePath);
            bitmap.EndInit();
            return bitmap;
        }

        private void OpenImage(string path)
        {
            if (File.Exists(path)) {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
        }

        private void BtnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            // _isTempPreviewMode ではなく、画像が存在しない時だけブロックする
            if (!HasLastCapture) return;
            OpenImage(_lastCapturedPath);
        }

        // スプリットボタン左「コピー」本体：詳細設定の既定対象でコピーする。
        private void BtnCopyClipboard_Click(object sender, RoutedEventArgs e)
        {
            var target = CopyTargetResolver.Parse(_settingsManager.Current.CopySource);
            CopyToClipboard(target, isAuto: false);
        }

        // スプリットボタン ▽ メニュー「一時プレビューをコピー」
        private void MenuCopyTempPreview_Click(object sender, RoutedEventArgs e)
            => CopyToClipboard(CopyTarget.TempPreview, isAuto: false);

        // スプリットボタン ▽ メニュー「最後に保存した画像をコピー」
        private void MenuCopyLastSaved_Click(object sender, RoutedEventArgs e)
            => CopyToClipboard(CopyTarget.LastSaved, isAuto: false);

        /// <summary>指定した対象の画像をクリップボードにコピーする。対象が無ければ警告ログを出す。</summary>
        private void CopyToClipboard(CopyTarget target, bool isAuto)
        {
            try
            {
                string? path = CopyTargetResolver.Resolve(
                    target,
                    TempPreviewPath, File.Exists(TempPreviewPath),
                    _lastCapturedPath, HasLastCapture);

                if (path == null)
                {
                    Log(target == CopyTarget.TempPreview
                        ? "一時プレビューがまだありません。"
                        : "保存された画像がまだありません。");
                    return;
                }

                Clipboard.SetImage(LoadBitmap(path));
                Log(isAuto ? "画像をクリップボードに自動コピーしました。" : "画像をクリップボードにコピーしました。");
            }
            catch (Exception ex) { Log($"【エラー】 コピー失敗: {ex.Message}"); }
        }

        // スプリットボタン右「▽」：付属の ContextMenu をボタン位置に開く。
        private void BtnCopyDropdown_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                btn.ContextMenu.IsOpen = true;
            }
        }

        private void BtnDeleteFile_Click(object sender, RoutedEventArgs e)
        {
            if (!HasLastCapture) return;

            MessageBoxResult result = MessageBox.Show(
                "一番最後にキャプチャした画像ファイルをPCから完全に削除しますか？",
                "画像の削除確認",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes) {
                try {
                    string fileName = Path.GetFileName(_lastCapturedPath);
                    File.Delete(_lastCapturedPath);
                    Log($"【削除】 画像ファイルを削除しました: {fileName}");

                    ImgPreview.Source = null;
                    UpdateCutOverlay(forceHide: true);
                    if (PreviewWatermarkBorder != null) PreviewWatermarkBorder.Visibility = Visibility.Collapsed;

                    TxtDeleteMessage.Visibility = Visibility.Visible;

                    _lastCapturedPath = "";

                    // 削除後はファイルが存在しなくなるため、ボタンを無効状態の見た目に戻す
                    SetActionButtonsState(false);
                    UpdateToolTips(); // 削除後にツールチップも更新
                }
                catch (Exception ex) {
                    Log($"【エラー】 ファイルの削除に失敗しました: {ex.Message}");
                }
            }
            else {
                Log("画像の削除をキャンセルしました。");
            }
        }
    }
}