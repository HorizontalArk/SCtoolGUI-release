using System.IO;
using SCtoolGui;

namespace SCtoolGui.Tests
{
    public class SettingsPathTests
    {
        // 各テストで隔離された一時ディレクトリを使う
        private static (string appData, string legacy) MakeTempDirs()
        {
            string root = Path.Combine(Path.GetTempPath(), "sctool_test_" + Path.GetRandomFileName());
            string appData = Path.Combine(root, "appdata");
            string legacy = Path.Combine(root, "legacy");
            Directory.CreateDirectory(appData);
            Directory.CreateDirectory(legacy);
            return (appData, legacy);
        }

        [Fact]
        public void 新パスは_AppData配下のSCtoolGui_cut_settings_json()
        {
            var (appData, legacy) = MakeTempDirs();

            string path = SettingsManager.ResolveSettingsFile(appData, legacy);

            Assert.Equal(Path.Combine(appData, "SCtoolGui", "cut_settings.json"), path);
            Assert.True(Directory.Exists(Path.Combine(appData, "SCtoolGui")));
        }

        [Fact]
        public void 旧位置に設定があれば新パスへ移行される()
        {
            var (appData, legacy) = MakeTempDirs();
            File.WriteAllText(Path.Combine(legacy, "cut_settings.json"), "{\"HotkeyKey\":83}");

            string path = SettingsManager.ResolveSettingsFile(appData, legacy);

            Assert.True(File.Exists(path));
            Assert.Contains("83", File.ReadAllText(path));
        }

        [Fact]
        public void 新パスに既存があれば旧位置で上書きしない()
        {
            var (appData, legacy) = MakeTempDirs();
            string newPath = Path.Combine(appData, "SCtoolGui", "cut_settings.json");
            Directory.CreateDirectory(Path.GetDirectoryName(newPath)!);
            File.WriteAllText(newPath, "NEW");
            File.WriteAllText(Path.Combine(legacy, "cut_settings.json"), "OLD");

            SettingsManager.ResolveSettingsFile(appData, legacy);

            Assert.Equal("NEW", File.ReadAllText(newPath));
        }
    }
}
