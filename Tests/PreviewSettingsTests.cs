using System.Text.Json;
using SCtoolGui;

namespace SCtoolGui.Tests
{
    public class PreviewSettingsTests
    {
        [Fact]
        public void プレビュー向きの既定はHorizontal()
        {
            Assert.Equal("Horizontal", new AppSettings().PreviewOrientation);
        }

        [Fact]
        public void 縦時の左右の既定はRight()
        {
            Assert.Equal("Right", new AppSettings().VerticalPreviewSide);
        }

        [Fact]
        public void 自動切替の既定はPrompt()
        {
            Assert.Equal("Prompt", new AppSettings().PreviewAutoSwitch);
        }

        [Fact]
        public void 縦横プロパティはシリアライズ往復で保持される()
        {
            var s = new AppSettings
            {
                PreviewOrientation = "Vertical",
                VerticalPreviewSide = "Left",
                PreviewAutoSwitch = "Force",
                VerticalWindowWidth = 500,
                VerticalWindowHeight = 900,
            };
            var back = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(s))!;
            Assert.Equal("Vertical", back.PreviewOrientation);
            Assert.Equal("Left", back.VerticalPreviewSide);
            Assert.Equal("Force", back.PreviewAutoSwitch);
            Assert.Equal(500, back.VerticalWindowWidth);
            Assert.Equal(900, back.VerticalWindowHeight);
        }

        [Fact]
        public void 旧設定JSONに項目が無ければ既定になる()
        {
            var back = JsonSerializer.Deserialize<AppSettings>("{\"SaveDirectory\":\"C:/x\"}")!;
            Assert.Equal("Horizontal", back.PreviewOrientation);
            Assert.Equal("Right", back.VerticalPreviewSide);
            Assert.Equal("Prompt", back.PreviewAutoSwitch);
        }
    }
}
