using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.IO;

namespace SCtoolGui
{
    public partial class MainWindow
    {
        private Point? _dragStartPoint = null;

        /// <summary>カット境界線を掴めるとみなす、線からの距離(px)。</summary>
        private const double CutLineGrabTolerance = 15;

        /// <summary>Shift併用時のカット量の増減ステップ。</summary>
        private const int CutStepWithShift = 10;
        private const int CutStepDefault = 1;

        /// <summary>カット量を増減し、0未満にならないよう丸めて反映する。</summary>
        private void AdjustCutValue(int delta)
        {
            if (!int.TryParse(TxtTopCut.Text, out int val)) return;
            TxtTopCut.Text = Math.Max(0, val + delta).ToString();
        }

        /// <summary>画像の有無に応じたプレビュー領域の既定カーソルを返す。</summary>
        private Cursor DefaultPreviewCursor => HasLastCapture ? Cursors.Hand : Cursors.Arrow;

        // -------------------------------------------------------------------
        // キーボード入力
        // -------------------------------------------------------------------
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (ChkCutTab.IsChecked != true) return;

            if (!TxtTopCut.IsFocused && !PreviewContentGrid.IsMouseOver) return;
            if (e.Key != Key.Up && e.Key != Key.Down) return;

            int step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? CutStepWithShift : CutStepDefault;
            AdjustCutValue(e.Key == Key.Up ? -step : step);
            e.Handled = true;
        }

        // -------------------------------------------------------------------
        // UIコントロール系イベント
        // -------------------------------------------------------------------
        
        private void BtnRefreshPreview_Click(object sender, RoutedEventArgs e)
        {
            UpdateTempPreview();
        }

        private void TxtTopCut_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            TxtTopCut.Text = "0";
            e.Handled = true;
        }

        private void TxtTopCut_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            AdjustCutValue(e.Delta > 0 ? CutStepDefault : -CutStepDefault);
            e.Handled = true;
        }

        private void TxtTopCut_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isFinalOutputMode)
            {
                UpdateTempPreview();
                return;
            }

            // 数値として読めない入力中の状態では、直前のオーバーレイ位置を維持する
            if (int.TryParse(TxtTopCut?.Text, out _)) UpdateCutOverlay();
        }

        private void ChkCutTab_Click(object sender, RoutedEventArgs e)
        {
            bool isOn = ChkCutTab.IsChecked == true;
            Log(LogMessages.TopCutChanged(isOn ? "ON" : "OFF"));

            if (ImgPreview.Source != null) UpdateCutOverlay();

            UpdateToolTips();
            UpdateTempPreview();
        }

        // -------------------------------------------------------------------
        // プレビュー画面上でのマウス操作
        // -------------------------------------------------------------------
        private void SetCutValueFromY(double y)
        {
            if (ImgPreview.Source == null) return;
            int maxH = (int)ImgPreview.ActualHeight;
            if (maxH == 0) maxH = (int)ImgPreview.Source.Height;

            int newCut = (int)Math.Round(y);
            if (newCut < 0) newCut = 0;
            if (maxH > 0 && newCut > maxH) newCut = maxH;

            TxtTopCut.Text = newCut.ToString();
        }

        private void PreviewContentGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 一時プレビュー中かに関わらず、保存された画像があればダブルクリックで開けるようにする
            bool hasLastCapture = HasLastCapture;
            if (e.ClickCount == 2 && hasLastCapture)
            {
                OpenImage(_lastCapturedPath);
                return;
            }

            if (ImgPreview.Source == null) return;
            if (_isFinalOutputMode) return;

            bool isCutOn = ChkCutTab.IsChecked == true;
            double mouseY = e.GetPosition(PreviewContentGrid).Y;

            if (isCutOn && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                SetCutValueFromY(mouseY);
                _isDraggingCutLine = true;
                PreviewContentGrid.CaptureMouse();
                e.Handled = true;
            }
            else if (isCutOn && IsNearCutLine(mouseY))
            {
                _isDraggingCutLine = true;
                PreviewContentGrid.CaptureMouse();
                e.Handled = true;
            }
            else if (!_isTempPreviewMode && hasLastCapture)
            {
                _dragStartPoint = e.GetPosition(null);
            }
        }

        /// <summary>指定Y座標がカット境界線のドラッグ判定範囲内か。</summary>
        private bool IsNearCutLine(double mouseY) => Math.Abs(mouseY - CurrentCutValue) <= CutLineGrabTolerance;

        private void PreviewContentGrid_MouseMove(object sender, MouseEventArgs e)
        {
            if (ImgPreview.Source == null) return;

            double mouseY = e.GetPosition(PreviewContentGrid).Y;
            bool isCutOn = ChkCutTab.IsChecked == true;

            if (_isDraggingCutLine)
            {
                SetCutValueFromY(mouseY);
            }
            else
            {
                if (isCutOn && !_isFinalOutputMode)
                {
                    // 線から離れた時は、画像があれば指カーソル、なければ矢印にする
                    PreviewContentGrid.Cursor = IsNearCutLine(mouseY) ? Cursors.SizeNS : DefaultPreviewCursor;
                }

                if (e.LeftButton == MouseButtonState.Pressed && !_isTempPreviewMode && _dragStartPoint.HasValue)
                {
                    Point currentPos = e.GetPosition(null);
                    Vector diff = _dragStartPoint.Value - currentPos;

                    if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                        Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                    {
                        if (HasLastCapture)
                        {
                            var dataObject = new DataObject(DataFormats.FileDrop, new string[] { _lastCapturedPath });
                            DragDrop.DoDragDrop(PreviewContentGrid, dataObject, DragDropEffects.Copy);
                        }
                        _dragStartPoint = null;
                    }
                }
            }
        }

        private void PreviewContentGrid_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingCutLine)
            {
                _isDraggingCutLine = false;
                PreviewContentGrid.ReleaseMouseCapture();

                // ドラッグを離した時のカーソル復元
                PreviewContentGrid.Cursor = DefaultPreviewCursor;

                e.Handled = true;
            }
            
            _dragStartPoint = null;
        }

        private void PreviewContentGrid_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (ChkCutTab.IsChecked != true) return;

            // プレビュー上ではホイール上回転でカット位置を上へ（＝値を減らす）
            int step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? CutStepWithShift : CutStepDefault;
            AdjustCutValue(e.Delta > 0 ? -step : step);
            e.Handled = true;
        }

        private void ImgPreview_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (CutBoundaryLine != null && ImgPreview.Source != null)
            {
                double imgWidth = e.NewSize.Width;
                CutBoundaryLine.X1 = -50;
                CutBoundaryLine.X2 = imgWidth + 50;
            }
        }
    }
}