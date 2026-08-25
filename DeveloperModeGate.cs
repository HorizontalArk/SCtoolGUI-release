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
        // 合言葉 "SCtoolGui-dev-3f9c1a7e5b2d48e6a0c9f14b7d8e2a65" の SHA256(16進小文字)。
        // 合言葉の平文は置かない。developer.key にこの合言葉を書くと解錠される。
        private const string ExpectedHashHex =
            "58146eb7ea8fe9429cca1ca42a564920d597e984466446762ba369e97c02656d";

        /// <summary>ファイル中身のハッシュが埋め込み値と一致すれば true。null/空/不一致は false。</summary>
        public static bool IsUnlocked(string? fileContent)
        {
            if (string.IsNullOrWhiteSpace(fileContent)) return false;
            string hex = Sha256Hex(fileContent.Trim());
            return string.Equals(hex, ExpectedHashHex, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>文字列の SHA256 を 16進小文字で返す。</summary>
        public static string Sha256Hex(string s)
        {
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(s));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
