using System.Windows;
using System.Windows.Input;

namespace SCtoolGui
{
    /// <summary>ターゲットの呼び名（DisplayName）を編集するダイアログ。</summary>
    public partial class RenameWindow : Window
    {
        private readonly string _defaultName;

        /// <summary>入力された呼び名。OKで閉じた場合のみ有効。</summary>
        public string ResultName { get; private set; } = "";

        /// <param name="currentName">現在の呼び名。</param>
        /// <param name="defaultName">「既定に戻す」で復元する名前（実行ファイル名由来）。</param>
        public RenameWindow(string currentName, string defaultName)
        {
            InitializeComponent();
            _defaultName = defaultName;

            TxtName.Text = currentName;
            TxtName.SelectAll();
            TxtName.Focus();
        }

        private void TxtName_KeyDown(object sender, KeyEventArgs e)
        {
            if (TxtError.Visibility == Visibility.Visible) TxtError.Visibility = Visibility.Collapsed;
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            TxtName.Text = _defaultName;
            TxtName.SelectAll();
            TxtName.Focus();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            string name = TxtName.Text.Trim();

            if (string.IsNullOrEmpty(name))
            {
                ShowError("呼び名を入力してください。");
                return;
            }

            // フォルダ名にも使うため、パスとして成立しない名前は弾く
            if (!IsValidFolderName(name))
            {
                ShowError("フォルダ名に使えない文字が含まれています: \\ / : * ? \" < > |");
                return;
            }

            ResultName = name;
            DialogResult = true;
        }

        private void ShowError(string message)
        {
            TxtError.Text = message;
            TxtError.Visibility = Visibility.Visible;
            TxtName.Focus();
        }

        /// <summary>フォルダ名として使える文字だけで構成されているか。</summary>
        public static bool IsValidFolderName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            return name.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) < 0;
        }
    }
}
