using System;
using System.Collections.Generic;
using System.Linq;

namespace SCtoolGui
{
    /// <summary>照合に使えるウィンドウの最小情報。Win32に触れずにテストできるよう、値だけを持つ。</summary>
    public interface IWindowSnapshot
    {
        string Title { get; }
        IntPtr Handle { get; }
        string ExecutablePath { get; }
    }

    /// <summary>どの手段でターゲットを再特定できたか。</summary>
    public enum MatchKind
    {
        /// <summary>見つからなかった（未起動）。</summary>
        None,

        /// <summary>前回と同じウィンドウハンドルが生きていた。タイトル変更の影響を受けない最も確実な一致。</summary>
        Handle,

        /// <summary>実行ファイルパスが一致した。アプリを再起動した場合の経路。</summary>
        ExecutablePath,

        /// <summary>タイトルが一致した。exeパスを取得できない環境向けの後方互換経路。</summary>
        Title,
    }

    /// <summary>ターゲット照合の結果。</summary>
    public readonly struct MatchResult
    {
        public MatchResult(IWindowSnapshot? window, MatchKind kind)
        {
            Window = window;
            Kind = kind;
        }

        public IWindowSnapshot? Window { get; }
        public MatchKind Kind { get; }
        public bool IsMatched => Window != null;

        public static readonly MatchResult NotFound = new MatchResult(null, MatchKind.None);
    }

    /// <summary>
    /// 保存済みターゲットを、現在起動中のウィンドウ群の中から再特定する。
    /// ウィンドウタイトルはアプリの状態で変化するため識別子として信頼できない。
    /// そこで ハンドル → 実行ファイルパス → タイトル の順に照合してタイトル変更を吸収する。
    /// 副作用を持たない純粋関数として実装してある（テスト容易性のため）。
    /// </summary>
    public static class WindowMatcher
    {
        /// <summary>
        /// ターゲットに対応するウィンドウを探す。
        /// </summary>
        /// <param name="target">保存済みのターゲット情報。null の場合は未選択とみなす。</param>
        /// <param name="lastKnownHandle">
        /// 前回このターゲットに対して使っていたハンドル。生きていれば最優先で採用する。
        /// </param>
        /// <param name="windows">現在起動中のウィンドウ一覧。</param>
        /// <param name="isHandleAlive">ハンドルが生存しているかの判定。テストから差し替えられるようにしてある。</param>
        public static MatchResult Match(
            TargetInfo? target,
            IntPtr lastKnownHandle,
            IReadOnlyList<IWindowSnapshot> windows,
            Func<IntPtr, bool>? isHandleAlive = null)
        {
            if (target == null || windows == null || windows.Count == 0) return MatchResult.NotFound;

            // 1. 前回のハンドルがまだ一覧に存在するか。
            //    タイトルが変わっていても同じウィンドウを指し続けられる、最も確実な経路。
            if (lastKnownHandle != IntPtr.Zero)
            {
                bool alive = isHandleAlive?.Invoke(lastKnownHandle) ?? true;
                if (alive)
                {
                    var byHandle = windows.FirstOrDefault(w => w.Handle == lastKnownHandle);
                    if (byHandle != null) return new MatchResult(byHandle, MatchKind.Handle);
                }
            }

            // 2. 実行ファイルパスの一致。アプリを再起動するとハンドルは変わるが、これは変わらない。
            if (!string.IsNullOrEmpty(target.ExecutablePath))
            {
                var sameExe = windows
                    .Where(w => PathEquals(w.ExecutablePath, target.ExecutablePath))
                    .ToList();

                if (sameExe.Count == 1) return new MatchResult(sameExe[0], MatchKind.ExecutablePath);

                if (sameExe.Count > 1)
                {
                    // 多重起動時は、前回のタイトルに一致するものを優先し、
                    // 決め手が無ければ先頭を採る（呼び出し側が一覧から選び直せる）。
                    var byTitle = sameExe.FirstOrDefault(w => w.Title == target.LastKnownTitle);
                    return new MatchResult(byTitle ?? sameExe[0], MatchKind.ExecutablePath);
                }
            }

            // 3. タイトル一致。exeパスを取得できなかったターゲット向けの後方互換経路。
            if (!string.IsNullOrEmpty(target.LastKnownTitle))
            {
                var byTitle = windows.FirstOrDefault(w => w.Title == target.LastKnownTitle);
                if (byTitle != null) return new MatchResult(byTitle, MatchKind.Title);
            }

            return MatchResult.NotFound;
        }

        /// <summary>実行ファイルパスの比較。Windowsのパスは大文字小文字を区別しない。</summary>
        public static bool PathEquals(string? a, string? b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            return string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
