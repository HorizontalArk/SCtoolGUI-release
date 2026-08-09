using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SCtoolGui
{
    /// <summary>
    /// キャプチャ対象アプリの永続情報。
    /// ウィンドウタイトルはアプリの状態で変化するため、実行ファイルパスを識別子として使う。
    /// </summary>
    public class TargetInfo
    {
        /// <summary>実行ファイルのフルパス。このターゲットの識別子。</summary>
        public string ExecutablePath { get; set; } = "";

        /// <summary>保存フォルダ名や呼び名に使う名前。初期値は exe 名（安定・パス安全）で、ユーザーが変更できる。
        /// 状態表示に出す「ウィンドウ名」とは別物（そちらは LastKnownTitle を使う）。</summary>
        public string DisplayName { get; set; } = "";

        /// <summary>最後に確認したウィンドウタイトル。exeパス照合が使えない場合のフォールバックに使う。</summary>
        public string LastKnownTitle { get; set; } = "";

        /// <summary>このターゲットに対する上部カット量(px)。</summary>
        public int TopCut { get; set; }

        /// <summary>
        /// 保存済みターゲットを一意に指すキー。
        /// exeパスが取れなかった旧データはタイトルをキーにするため、その差を吸収する。
        /// </summary>
        public string Key => !string.IsNullOrEmpty(ExecutablePath) ? ExecutablePath : LastKnownTitle;

        /// <summary>
        /// 呼び名(保存フォルダ名)の初期値を作る。exe名を優先し、
        /// exeパスが取得できない場合のみウィンドウタイトルにフォールバックする。
        /// </summary>
        public static string DeriveDisplayName(string executablePath, string windowTitle)
        {
            if (!string.IsNullOrEmpty(executablePath))
            {
                string name = Path.GetFileNameWithoutExtension(executablePath);
                if (!string.IsNullOrEmpty(name)) return name;
            }
            return windowTitle;
        }
    }

    /// <summary>保存済みターゲットの集合に対する検索・登録操作。副作用を持たない純粋関数群。</summary>
    public static class TargetRegistry
    {
        /// <summary>exeパス（無ければタイトル）で保存済みターゲットを探す。</summary>
        public static TargetInfo? Find(IEnumerable<TargetInfo> targets, string? executablePath, string? title)
        {
            if (targets == null) return null;

            if (!string.IsNullOrEmpty(executablePath))
            {
                var byPath = targets.FirstOrDefault(t => WindowMatcher.PathEquals(t.ExecutablePath, executablePath));
                if (byPath != null) return byPath;
            }

            if (!string.IsNullOrEmpty(title))
            {
                // exeパスを持たない旧データ、または取得に失敗したターゲット向け
                return targets.FirstOrDefault(t => string.IsNullOrEmpty(t.ExecutablePath) && t.LastKnownTitle == title);
            }

            return null;
        }

        /// <summary>
        /// ウィンドウに対応するターゲットを取得し、無ければ新規作成して一覧に加える。
        /// 既存が見つかった場合は最新のタイトルを記録し直す。
        /// </summary>
        public static TargetInfo GetOrAdd(List<TargetInfo> targets, string executablePath, string title)
        {
            var existing = Find(targets, executablePath, title);
            if (existing != null)
            {
                existing.LastKnownTitle = title;

                // 旧データ（タイトルのみで登録されたもの）に、取得できたexeパスを補完する
                if (string.IsNullOrEmpty(existing.ExecutablePath) && !string.IsNullOrEmpty(executablePath))
                {
                    existing.ExecutablePath = executablePath;
                }
                return existing;
            }

            var created = new TargetInfo
            {
                ExecutablePath = executablePath ?? "",
                LastKnownTitle = title ?? "",
                DisplayName = TargetInfo.DeriveDisplayName(executablePath ?? "", title ?? ""),
            };
            targets.Add(created);
            return created;
        }
    }
}
