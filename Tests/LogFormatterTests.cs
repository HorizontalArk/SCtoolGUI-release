using SCtoolGui;

namespace SCtoolGui.Tests
{
    public class LogFormatterTests
    {
        [Fact]
        public void 情報はプレフィックスを付けずそのまま返す()
        {
            string result = LogFormatter.Format(LogLevel.Info, "ウィンドウを切り替えました。");
            Assert.Equal("ウィンドウを切り替えました。", result);
        }

        [Fact]
        public void 成功は成功タグを付ける()
        {
            string result = LogFormatter.Format(LogLevel.Success, "保存しました。");
            Assert.Equal("[成功] 保存しました。", result);
        }

        [Fact]
        public void 警告は警告タグを付ける()
        {
            string result = LogFormatter.Format(LogLevel.Warning, "起動していません。");
            Assert.Equal("[警告] 起動していません。", result);
        }

        [Fact]
        public void エラーはエラータグを付ける()
        {
            string result = LogFormatter.Format(LogLevel.Error, "失敗しました。");
            Assert.Equal("[エラー] 失敗しました。", result);
        }

        [Fact]
        public void 対象名は角括弧で囲んで整形する()
        {
            string result = LogFormatter.Target("ELDEN RING");
            Assert.Equal("[ELDEN RING]", result);
        }

        [Fact]
        public void 対象名が空なら未選択プレースホルダを返す()
        {
            string result = LogFormatter.Target("");
            Assert.Equal("[対象未選択]", result);
        }
    }
}
