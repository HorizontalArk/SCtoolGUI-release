using SCtoolGui;

namespace SCtoolGui.Tests
{
    public class ReleaseLinksTests
    {
        [Fact]
        public void バージョンからReleaseページURLを組み立てる()
        {
            Assert.Equal(
                "https://github.com/HorizontalArk/SCtoolGUI-release/releases/tag/v1.2.0",
                ReleaseLinks.ReleasePageUrl("https://github.com/HorizontalArk/SCtoolGUI-release", "1.2.0"));
        }

        [Fact]
        public void リポジトリURL末尾のスラッシュは無視される()
        {
            Assert.Equal(
                "https://github.com/HorizontalArk/SCtoolGUI-release/releases/tag/v1.2.0",
                ReleaseLinks.ReleasePageUrl("https://github.com/HorizontalArk/SCtoolGUI-release/", "1.2.0"));
        }

        [Fact]
        public void プレリリースのバージョン文字列もそのまま使われる()
        {
            Assert.Equal(
                "https://github.com/HorizontalArk/SCtoolGUI-release/releases/tag/v1.2.0-pre",
                ReleaseLinks.ReleasePageUrl("https://github.com/HorizontalArk/SCtoolGUI-release", "1.2.0-pre"));
        }
    }
}
