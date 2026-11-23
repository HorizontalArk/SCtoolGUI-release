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
            string result = CaptureFileName.Build("ELDEN RING", "{yyyyMMdd_hhmmss}");
            Assert.Equal("ELDEN RING_{yyyyMMdd_hhmmss}.jpg", result);
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
        public void 時刻プレースホルダ定数は波括弧付きのプレースホルダ形式()
        {
            Assert.Equal("{yyyyMMdd_hhmmss}", CaptureFileName.TimePlaceholder);
        }

        [Fact]
        public void 時刻先頭なら時刻_保存名の順で連結する()
        {
            string result = CaptureFileName.Build("MyGame", "20260817_143052", CaptureFormat.Png, CaptureFileNameOrder.TimeFirst);
            Assert.Equal("20260817_143052_MyGame.png", result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Unknown")]
        [InlineData(CaptureFileNameOrder.NameFirst)]
        public void 時刻先頭以外は保存名_時刻の順(string? order)
        {
            string result = CaptureFileName.Build("MyGame", "20260817_143052", CaptureFormat.Jpeg, order);
            Assert.Equal("MyGame_20260817_143052.jpg", result);
        }

        [Fact]
        public void プレビューでは時刻先頭ならプレースホルダを先頭に置く()
        {
            string result = CaptureFileName.Build("MyGame", CaptureFileName.TimePlaceholder, CaptureFormat.Jpeg, CaptureFileNameOrder.TimeFirst);
            Assert.Equal("{yyyyMMdd_hhmmss}_MyGame.jpg", result);
        }
    }
}
