namespace SCtoolGui
{
    /// <summary>
    /// ホットキーの仮想キーコードを画面表示用の文字列に変換する純粋ロジック。
    /// キャプチャボタンのキーキャップ表示(MainWindow)と詳細設定のキー選択(SettingsWindow)の
    /// 両方で同じ変換が必要なため、ここに共通化してある。
    /// </summary>
    public static class HotKeyDisplay
    {
        /// <summary>0x70〜0x7B(VK_F1〜VK_F12)なら "F1"〜"F12"、それ以外は文字そのもの。</summary>
        public static string KeyText(uint virtualKey)
            => (virtualKey >= 0x70 && virtualKey <= 0x7B)
                ? $"F{virtualKey - 0x70 + 1}"
                : ((char)virtualKey).ToString();
    }
}
