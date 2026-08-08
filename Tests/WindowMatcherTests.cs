using System;
using System.Collections.Generic;
using SCtoolGui;

namespace SCtoolGui.Tests
{
    /// <summary>テスト用のウィンドウ情報。実際のWin32ウィンドウを用意せずに照合を検証する。</summary>
    internal sealed class FakeWindow : IWindowSnapshot
    {
        public string Title { get; init; } = "";
        public IntPtr Handle { get; init; }
        public string ExecutablePath { get; init; } = "";
    }

    public class WindowMatcherTests
    {
        private const string GamePath = @"C:\Games\ELDEN RING\eldenring.exe";
        private const string BrowserPath = @"C:\Program Files\Chrome\chrome.exe";

        private static TargetInfo GameTarget(string lastTitle = "ELDEN RING") => new TargetInfo
        {
            ExecutablePath = GamePath,
            DisplayName = "eldenring",
            LastKnownTitle = lastTitle,
        };

        private static List<IWindowSnapshot> Windows(params IWindowSnapshot[] windows) => new(windows);

        // --- 本命のシナリオ: ゲーム内の状態でタイトルが変わっても見失わない ---

        [Fact]
        public void タイトルが変わってもハンドルが生きていれば捕捉できる()
        {
            var target = GameTarget("ELDEN RING");
            var handle = new IntPtr(1234);

            // ゲーム側がタイトルを書き換えた状態
            var windows = Windows(new FakeWindow
            {
                Title = "ELDEN RING - 王城ストームヴィル",
                Handle = handle,
                ExecutablePath = GamePath,
            });

            var result = WindowMatcher.Match(target, handle, windows);

            Assert.True(result.IsMatched);
            Assert.Equal(MatchKind.Handle, result.Kind);
            Assert.Equal("ELDEN RING - 王城ストームヴィル", result.Window!.Title);
        }

        [Fact]
        public void アプリ再起動でハンドルが変わってもexeパスで捕捉できる()
        {
            var target = GameTarget();
            var oldHandle = new IntPtr(1234);

            // 再起動後は別のハンドルになり、タイトルも変わっている
            var windows = Windows(new FakeWindow
            {
                Title = "ELDEN RING - 別の場所",
                Handle = new IntPtr(9999),
                ExecutablePath = GamePath,
            });

            var result = WindowMatcher.Match(target, oldHandle, windows, _ => false);

            Assert.True(result.IsMatched);
            Assert.Equal(MatchKind.ExecutablePath, result.Kind);
            Assert.Equal(new IntPtr(9999), result.Window!.Handle);
        }

        [Fact]
        public void 死んだハンドルは無視して再照合する()
        {
            var target = GameTarget();
            var deadHandle = new IntPtr(1234);

            var windows = Windows(new FakeWindow
            {
                Title = "ELDEN RING",
                Handle = new IntPtr(5678),
                ExecutablePath = GamePath,
            });

            // ハンドルは死んでいると報告させる
            var result = WindowMatcher.Match(target, deadHandle, windows, h => h != deadHandle);

            Assert.Equal(MatchKind.ExecutablePath, result.Kind);
            Assert.Equal(new IntPtr(5678), result.Window!.Handle);
        }

        [Fact]
        public void ハンドルが一覧に無ければexeパスへフォールバックする()
        {
            var target = GameTarget();

            // ハンドルは生きていると報告されるが、一覧には存在しない
            var windows = Windows(new FakeWindow
            {
                Title = "ELDEN RING",
                Handle = new IntPtr(777),
                ExecutablePath = GamePath,
            });

            var result = WindowMatcher.Match(target, new IntPtr(1234), windows, _ => true);

            Assert.Equal(MatchKind.ExecutablePath, result.Kind);
        }

        // --- フォールバック段の検証 ---

        [Fact]
        public void exeパスを持たない旧データはタイトルで捕捉できる()
        {
            var target = new TargetInfo
            {
                ExecutablePath = "",
                LastKnownTitle = "メモ帳",
                DisplayName = "メモ帳",
            };

            var windows = Windows(new FakeWindow
            {
                Title = "メモ帳",
                Handle = new IntPtr(42),
                ExecutablePath = @"C:\Windows\notepad.exe",
            });

            var result = WindowMatcher.Match(target, IntPtr.Zero, windows);

            Assert.True(result.IsMatched);
            Assert.Equal(MatchKind.Title, result.Kind);
        }

        [Fact]
        public void 対象が起動していなければ見つからない()
        {
            var target = GameTarget();

            var windows = Windows(new FakeWindow
            {
                Title = "まったく別のアプリ",
                Handle = new IntPtr(1),
                ExecutablePath = BrowserPath,
            });

            var result = WindowMatcher.Match(target, IntPtr.Zero, windows);

            Assert.False(result.IsMatched);
            Assert.Equal(MatchKind.None, result.Kind);
        }

        [Fact]
        public void ウィンドウ一覧が空なら見つからない()
        {
            var result = WindowMatcher.Match(GameTarget(), new IntPtr(1), Windows());

            Assert.False(result.IsMatched);
        }

        [Fact]
        public void ターゲット未選択なら見つからない()
        {
            var windows = Windows(new FakeWindow { Title = "何か", Handle = new IntPtr(1) });

            var result = WindowMatcher.Match(null, IntPtr.Zero, windows);

            Assert.False(result.IsMatched);
        }

        // --- 多重起動 ---

        [Fact]
        public void 多重起動時は前回のタイトルに一致するものを優先する()
        {
            var target = GameTarget("Chrome - 作業用");
            target.ExecutablePath = BrowserPath;

            var windows = Windows(
                new FakeWindow { Title = "Chrome - 調べ物", Handle = new IntPtr(1), ExecutablePath = BrowserPath },
                new FakeWindow { Title = "Chrome - 作業用", Handle = new IntPtr(2), ExecutablePath = BrowserPath });

            var result = WindowMatcher.Match(target, IntPtr.Zero, windows);

            Assert.Equal(MatchKind.ExecutablePath, result.Kind);
            Assert.Equal(new IntPtr(2), result.Window!.Handle);
        }

        [Fact]
        public void 多重起動でタイトルも一致しなければ先頭を返す()
        {
            var target = new TargetInfo { ExecutablePath = BrowserPath, LastKnownTitle = "もう存在しないタブ" };

            var windows = Windows(
                new FakeWindow { Title = "Chrome - A", Handle = new IntPtr(1), ExecutablePath = BrowserPath },
                new FakeWindow { Title = "Chrome - B", Handle = new IntPtr(2), ExecutablePath = BrowserPath });

            var result = WindowMatcher.Match(target, IntPtr.Zero, windows);

            Assert.True(result.IsMatched);
            Assert.Equal(new IntPtr(1), result.Window!.Handle);
        }

        [Fact]
        public void 多重起動でもハンドルが生きていればそれを維持する()
        {
            var target = new TargetInfo { ExecutablePath = BrowserPath, LastKnownTitle = "Chrome - A" };
            var handle = new IntPtr(2);

            var windows = Windows(
                new FakeWindow { Title = "Chrome - A", Handle = new IntPtr(1), ExecutablePath = BrowserPath },
                new FakeWindow { Title = "Chrome - B", Handle = handle, ExecutablePath = BrowserPath });

            var result = WindowMatcher.Match(target, handle, windows);

            // タイトルはAに一致するが、追跡中のハンドルBが優先される
            Assert.Equal(MatchKind.Handle, result.Kind);
            Assert.Equal(handle, result.Window!.Handle);
        }

        // --- パス比較 ---

        [Theory]
        [InlineData(@"C:\App\game.exe", @"c:\app\GAME.EXE", true)]
        [InlineData(@"C:\App\game.exe", @"  C:\App\game.exe  ", true)]
        [InlineData(@"C:\App\game.exe", @"C:\App\other.exe", false)]
        [InlineData("", @"C:\App\game.exe", false)]
        [InlineData(@"C:\App\game.exe", "", false)]
        [InlineData(null, null, false)]
        public void パス比較は大文字小文字と前後空白を無視する(string? a, string? b, bool expected)
        {
            Assert.Equal(expected, WindowMatcher.PathEquals(a, b));
        }

        [Fact]
        public void exeパスが空のウィンドウは誤って一致しない()
        {
            var target = new TargetInfo { ExecutablePath = "", LastKnownTitle = "対象" };

            // 権限不足などでexeパスを取得できなかったウィンドウ同士が一致してしまわないこと
            var windows = Windows(new FakeWindow { Title = "別のアプリ", Handle = new IntPtr(1), ExecutablePath = "" });

            var result = WindowMatcher.Match(target, IntPtr.Zero, windows);

            Assert.False(result.IsMatched);
        }
    }
}
