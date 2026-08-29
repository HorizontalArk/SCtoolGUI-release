using System;
using System.Security.Cryptography;
using System.Text;

namespace SCtoolGui
{
    /// <summary>
    /// 開発者モードの解錠判定。マーカーファイル(developer.key)の中身のハッシュが、
    /// コードに埋め込んだハッシュと一致すれば解錠する。
    /// public リポジトリのため合言葉の平文はコードに置かず、SHA256 ハッシュのみを持つ。
    /// 合言葉は長いランダム文字列で、ハッシュからの逆算は事実上不可能。
    /// </summary>
    public static class DeveloperModeGate
    {
        // 合言葉のSHA256(16進小文字)。合言葉の平文はコードに一切置かない(developer.keyにのみ持つ)。
        private const string ExpectedHashHex =
            "e105da4e2663a77638c92dd3838a8b9faf7d4f86d364844ee2dc9889d62aaeb2";

        /// <summary>ファイル中身のハッシュが埋め込み値と一致すれば true。null/空/不一致は false。</summary>
        public static bool IsUnlocked(string? fileContent)
            => IsUnlocked(fileContent, ExpectedHashHex);

        /// <summary>期待ハッシュを外部から指定できるオーバーロード(テスト用)。</summary>
        public static bool IsUnlocked(string? fileContent, string expectedHashHex)
        {
            if (string.IsNullOrWhiteSpace(fileContent)) return false;
            string hex = Sha256Hex(fileContent.Trim());
            return string.Equals(hex, expectedHashHex, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>文字列の SHA256 を 16進小文字で返す。</summary>
        public static string Sha256Hex(string s)
        {
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(s));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        /// <summary>
        /// 設定画面の「開発者」タブを表示すべきか。解錠済み(developer.key照合成功)かつ
        /// 開発者モードトグルがONのときのみ true。未解錠なら常に false。
        /// </summary>
        public static bool ShouldShowDeveloperTab(bool developerUnlocked, bool developerModeToggleOn)
            => developerUnlocked && developerModeToggleOn;
    }
}
