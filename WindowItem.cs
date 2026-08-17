using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace SCtoolGui
{
    /// <summary>
    /// 一覧・選択に使うウィンドウ1件のデータモデル。
    /// WindowManager(静的ユーティリティ)とは責務が別なので独立ファイルに分ける。
    /// </summary>
    public class WindowItem : IWindowSnapshot, INotifyPropertyChanged
    {
        private string _title = string.Empty;

        /// <summary>
        /// ウィンドウタイトル。アプリの状態で変化しうるため、識別子には使わない。
        /// ComboBox が閉じた状態でも表示が追随するよう、変更通知を発火する
        /// （選択ボックスは Items.Refresh では再描画されないため）。
        /// </summary>
        public string Title
        {
            get => _title;
            set
            {
                if (_title == value) return;
                _title = value;
                OnPropertyChanged();
            }
        }

        public IntPtr Handle { get; set; }

        /// <summary>実行ファイルのフルパス。取得できなかった場合は空。ターゲットの永続的な識別子。</summary>
        public string ExecutablePath { get; set; } = string.Empty;

        /// <summary>実行ファイル名（拡張子なし）。表示名の初期値に使う。</summary>
        public string ProcessName =>
            string.IsNullOrEmpty(ExecutablePath) ? string.Empty : Path.GetFileNameWithoutExtension(ExecutablePath);

        /// <summary>プロセス（exe）のアイコン。一覧表示に使う。取得できなければ null。</summary>
        public ImageSource? Icon { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
