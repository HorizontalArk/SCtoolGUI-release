using SCtoolGui;

namespace SCtoolGui.Tests
{
    public class CaptureFileNameTests
    {
        [Fact]
        public void ファイル名ベースと時刻部分を連結し拡張子を付ける()
        {
            string result = CaptureFileName.Build("MyGame", "20260817_143052");
            Assert.Equal("MyGame_20260817_143052.jpg", result);
        }

        [Fact]
        public void プレビュー用のプレースホルダ時刻もそのまま連結する()
        {
            string result = CaptureFileName.Build("ELDEN RING", "yyyymmdd_hhmmss");
            Assert.Equal("ELDEN RING_yyyymmdd_hhmmss.jpg", result);
        }

        [Fact]
        public void PNG形式ならpng拡張子を付ける()
        {
            string result = CaptureFileName.Build("MyGame", "20260817_143052", CaptureFormat.Png);
            Assert.Equal("MyGame_20260817_143052.png", result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Unknown")]
        public void 未知の保存形式はjpgとして扱う(string? format)
        {
            string result = CaptureFileName.Build("MyGame", "20260817_143052", format);
            Assert.Equal("MyGame_20260817_143052.jpg", result);
        }

        [Fact]
        public void 時刻プレースホルダ定数はyyyymmdd_hhmmss形式()
        {
            Assert.Equal("yyyymmdd_hhmmss", CaptureFileName.TimePlaceholder);
        }
    }
}
