using System.Text.Json;
using SCtoolGui;

namespace SCtoolGui.Tests
{
    public class SetupCompletedTests
    {
        [Fact]
        public void SetupCompletedの既定はfalse()
        {
            var s = new AppSettings();
            Assert.False(s.SetupCompleted);
        }

        [Fact]
        public void SetupCompletedはシリアライズ往復で保持される()
        {
            var s = new AppSettings { SetupCompleted = true };
            string json = JsonSerializer.Serialize(s);
            var back = JsonSerializer.Deserialize<AppSettings>(json)!;
            Assert.True(back.SetupCompleted);
        }

        [Fact]
        public void 旧設定JSONにフラグが無ければfalse扱い()
        {
            // 既存ユーザーの設定には SetupCompleted が無い
            string json = "{\"SaveDirectory\":\"C:/x\"}";
            var back = JsonSerializer.Deserialize<AppSettings>(json)!;
            Assert.False(back.SetupCompleted);
        }

        [Theory]
        [InlineData(false, true, true)]   // 未完了＆インストール済み → 出す
        [InlineData(true, true, false)]   // 完了済み → 出さない
        [InlineData(false, false, false)] // ポータブル/開発 → 出さない
        [InlineData(true, false, false)]
        public void 初回ウィザード表示判定(bool completed, bool installed, bool expected)
        {
            Assert.Equal(expected, SettingsManager.ShouldShowSetupWizard(completed, installed));
        }
    }
}
