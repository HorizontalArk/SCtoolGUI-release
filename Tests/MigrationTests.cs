using System.Collections.Generic;
using System.Linq;
using SCtoolGui;

namespace SCtoolGui.Tests
{
    /// <summary>旧形式（タイトルをキーにしたカット設定）から Targets への移行の検証。</summary>
    public class MigrationTests
    {
        private static AppSettings OldFormat() => new AppSettings
        {
            CutSettings = new Dictionary<string, int>
            {
                ["ELDEN RING"] = 32,
                ["メモ帳"] = 0,
            },
            LastSelectedWindow = "ELDEN RING",
        };

        [Fact]
        public void 旧カット設定がTargetsへ移行される()
        {
            var settings = OldFormat();

            SettingsManager.Migrate(settings);

            Assert.Equal(2, settings.Targets.Count);

            var game = settings.Targets.Single(t => t.LastKnownTitle == "ELDEN RING");
            Assert.Equal(32, game.TopCut);
            Assert.Equal("ELDEN RING", game.DisplayName);
        }

        [Fact]
        public void 移行直後のターゲットはexeパスを持たない()
        {
            var settings = OldFormat();

            SettingsManager.Migrate(settings);

            // 旧データにexeパスは無い。実際に捕捉できた時点で補完される。
            Assert.All(settings.Targets, t => Assert.Equal("", t.ExecutablePath));
        }

        [Fact]
        public void 最終選択ウィンドウが選択キーへ移行される()
        {
            var settings = OldFormat();

            SettingsManager.Migrate(settings);

            Assert.Equal("ELDEN RING", settings.LastSelectedTargetKey);
        }

        [Fact]
        public void カット設定が無い最終選択ウィンドウも移行される()
        {
            var settings = new AppSettings
            {
                CutSettings = new Dictionary<string, int>(),
                LastSelectedWindow = "設定したことがないアプリ",
            };

            SettingsManager.Migrate(settings);

            Assert.Equal("設定したことがないアプリ", settings.LastSelectedTargetKey);
            Assert.Single(settings.Targets);
        }

        [Fact]
        public void 移行は冪等で二重登録しない()
        {
            var settings = OldFormat();

            SettingsManager.Migrate(settings);
            int afterFirst = settings.Targets.Count;
            SettingsManager.Migrate(settings);

            Assert.Equal(afterFirst, settings.Targets.Count);
        }

        [Fact]
        public void 移行後も旧データは残す()
        {
            var settings = OldFormat();

            SettingsManager.Migrate(settings);

            // 旧バージョンで開き直しても設定が失われないようにするため
            Assert.NotEmpty(settings.CutSettings);
        }

        [Fact]
        public void 既に新形式なら選択キーを上書きしない()
        {
            var settings = new AppSettings
            {
                Targets = new List<TargetInfo>
                {
                    new TargetInfo { ExecutablePath = @"C:\App\game.exe", DisplayName = "game", TopCut = 10 },
                },
                LastSelectedTargetKey = @"C:\App\game.exe",
                LastSelectedWindow = "古いタイトル",
            };

            SettingsManager.Migrate(settings);

            Assert.Equal(@"C:\App\game.exe", settings.LastSelectedTargetKey);
        }

        [Fact]
        public void 空の設定でも例外にならない()
        {
            var settings = new AppSettings();

            SettingsManager.Migrate(settings);

            Assert.Empty(settings.Targets);
            Assert.Equal("", settings.LastSelectedTargetKey);
        }

        [Fact]
        public void nullを渡しても例外にならない()
        {
            SettingsManager.Migrate(null!);
        }
    }

    /// <summary>ターゲットの登録・検索の検証。</summary>
    public class TargetRegistryTests
    {
        private const string GamePath = @"C:\Games\eldenring.exe";

        [Fact]
        public void 新規ウィンドウはexe名を表示名として登録される()
        {
            var targets = new List<TargetInfo>();

            var target = TargetRegistry.GetOrAdd(targets, GamePath, "ELDEN RING");

            Assert.Single(targets);
            Assert.Equal("eldenring", target.DisplayName);
            Assert.Equal(GamePath, target.ExecutablePath);
        }

        [Fact]
        public void 同じexeなら再登録せず既存を返す()
        {
            var targets = new List<TargetInfo>();

            var first = TargetRegistry.GetOrAdd(targets, GamePath, "ELDEN RING");
            var second = TargetRegistry.GetOrAdd(targets, GamePath, "ELDEN RING - 別の場所");

            Assert.Single(targets);
            Assert.Same(first, second);
        }

        [Fact]
        public void 既存ターゲットは最新タイトルを記録し直す()
        {
            var targets = new List<TargetInfo>();
            TargetRegistry.GetOrAdd(targets, GamePath, "ELDEN RING");

            var updated = TargetRegistry.GetOrAdd(targets, GamePath, "ELDEN RING - ボス戦");

            Assert.Equal("ELDEN RING - ボス戦", updated.LastKnownTitle);
        }

        [Fact]
        public void ユーザーが変更した表示名はタイトル変更で上書きされない()
        {
            var targets = new List<TargetInfo>();
            var target = TargetRegistry.GetOrAdd(targets, GamePath, "ELDEN RING");
            target.DisplayName = "エルデンリング";

            TargetRegistry.GetOrAdd(targets, GamePath, "ELDEN RING - 全く別の名前");

            Assert.Equal("エルデンリング", target.DisplayName);
        }

        [Fact]
        public void 旧データにexeパスが補完される()
        {
            // 移行直後のターゲット（exeパス無し）
            var targets = new List<TargetInfo>
            {
                new TargetInfo { ExecutablePath = "", LastKnownTitle = "ELDEN RING", DisplayName = "ELDEN RING", TopCut = 32 },
            };

            var target = TargetRegistry.GetOrAdd(targets, GamePath, "ELDEN RING");

            Assert.Single(targets);
            Assert.Equal(GamePath, target.ExecutablePath);
            Assert.Equal(32, target.TopCut); // カット設定は保持される
        }

        [Fact]
        public void exeパスが違えば別ターゲットになる()
        {
            var targets = new List<TargetInfo>();

            TargetRegistry.GetOrAdd(targets, @"C:\A\app.exe", "同じ名前");
            TargetRegistry.GetOrAdd(targets, @"C:\B\app.exe", "同じ名前");

            Assert.Equal(2, targets.Count);
        }

        [Fact]
        public void 表示名の既定値はexe名から作られる()
        {
            Assert.Equal("eldenring", TargetInfo.DeriveDisplayName(GamePath, "無視される"));
        }

        [Fact]
        public void exeパスが無ければタイトルを表示名にする()
        {
            Assert.Equal("フォールバック", TargetInfo.DeriveDisplayName("", "フォールバック"));
        }

        [Fact]
        public void キーはexeパスを優先しなければタイトルを使う()
        {
            Assert.Equal(GamePath, new TargetInfo { ExecutablePath = GamePath, LastKnownTitle = "T" }.Key);
            Assert.Equal("T", new TargetInfo { ExecutablePath = "", LastKnownTitle = "T" }.Key);
        }
    }
}
