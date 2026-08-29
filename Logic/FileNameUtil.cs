using System;
using System.IO;
using System.Text;

namespace SCtoolGui
{
    /// <summary>フォルダ名・ファイル名として安全な文字列を作るユーティリティ。</summary>
    public static class FileNameUtil
    {
        /// <summary>フォルダ名が長くなりすぎないための上限。MAX_PATH 対策。</summary>
        private const int MaxLength = 80;

        /// <summary>
        /// 与えられた名前を、フォルダ名・ファイル名として安全な文字列に変換する。
        /// 無効文字は '_' に置換し、長すぎる場合は切り詰め、末尾の空白・ドットを除く。
        /// 結果が空になる場合は "Unknown" を返す。
        /// </summary>
        public static string ToSafeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Unknown";

            char[] invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            }

            string s = sb.ToString();
            if (s.Length > MaxLength) s = s.Substring(0, MaxLength);

            // Windows はフォルダ名・ファイル名末尾の空白とドットを許さない
            s = s.TrimEnd(' ', '.');

            return s.Length == 0 ? "Unknown" : s;
        }
    }
}
