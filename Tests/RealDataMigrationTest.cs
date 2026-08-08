using System.Text.Json;
using SCtoolGui;

namespace SCtoolGui.Tests
{
    /// <summary>実際のユーザー設定ファイルと同じ内容で移行を検証する。</summary>
    public class RealDataMigrationTests
    {
        // リポジトリ内の cut_settings.json の実データ
        private const string RealJson =
            """
            {"CutSettings":{"\u30BF\u30B9\u30AF \u30DE\u30CD\u30FC\u30B8\u30E3\u30FC":0,"gakumas":0},"SaveDirectory":"C:\\Users\\Example\\Pictures\\SCtool","HotkeyModifiers":6,"HotkeyKey":83,"LastSelectedWindow":"gakumas","LastAlwaysOnTop":false,"WindowLeft":12,"WindowTop":250,"AppTopmost":false,"SaveInWindowNameFolder":true}
            """;

        [Fact]
        public void 実データが壊れずに読み込めて移行できる()
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(RealJson);
            Assert.NotNull(settings);

            SettingsManager.Migrate(settings!);

            // 既存の設定値が保持されていること
            Assert.Equal(@"C:\Users\Example\Pictures\SCtool", settings!.SaveDirectory);
            Assert.Equal(6u, settings.HotkeyModifiers);
            Assert.Equal(83u, settings.HotkeyKey);
            Assert.True(settings.SaveInWindowNameFolder);
            Assert.Equal(12, settings.WindowLeft);

            // 2件のウィンドウがターゲットへ移行されていること
            Assert.Equal(2, settings.Targets.Count);
            Assert.Contains(settings.Targets, t => t.DisplayName == "gakumas");
            Assert.Contains(settings.Targets, t => t.DisplayName == "タスク マネージャー");

            // 選択状態が引き継がれていること
            Assert.Equal("gakumas", settings.LastSelectedTargetKey);
        }

        [Fact]
        public void 移行後に保存して読み直しても内容が保たれる()
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(RealJson)!;
            SettingsManager.Migrate(settings);

            // 保存 → 再読込 → 再移行（アプリの起動サイクル相当）
            string saved = JsonSerializer.Serialize(settings);
            var reloaded = JsonSerializer.Deserialize<AppSettings>(saved)!;
            SettingsManager.Migrate(reloaded);

            Assert.Equal(2, reloaded.Targets.Count);
            Assert.Equal("gakumas", reloaded.LastSelectedTargetKey);
            Assert.Equal(@"C:\Users\Example\Pictures\SCtool", reloaded.SaveDirectory);
        }
    }
}
