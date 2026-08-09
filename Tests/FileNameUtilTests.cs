using SCtoolGui;

namespace SCtoolGui.Tests
{
    public class FileNameUtilTests
    {
        [Fact]
        public void 無効文字はアンダースコアに置換される()
        {
            Assert.Equal("a_b_c", FileNameUtil.ToSafeName("a/b:c"));
        }

        [Fact]
        public void 末尾の空白とドットは除去される()
        {
            // Windows はフォルダ名末尾の空白・ドットを許さない
            Assert.Equal("name", FileNameUtil.ToSafeName("name. . "));
        }

        [Fact]
        public void 長すぎる名前は切り詰められる()
        {
            string result = FileNameUtil.ToSafeName(new string('x', 200));
            Assert.True(result.Length <= 80);
        }

        [Fact]
        public void 空文字は既定名になる()
        {
            Assert.Equal("Unknown", FileNameUtil.ToSafeName(""));
        }

        [Fact]
        public void 通常の名前はそのまま返る()
        {
            Assert.Equal("gakumas", FileNameUtil.ToSafeName("gakumas"));
        }
    }
}
