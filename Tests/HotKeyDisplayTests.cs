using SCtoolGui;

namespace SCtoolGui.Tests
{
    public class HotKeyDisplayTests
    {
        [Theory]
        [InlineData(0x70, "F1")]
        [InlineData(0x7B, "F12")]
        [InlineData(0x53, "S")]
        [InlineData(0x41, "A")]
        public void 仮想キーコードを表示文字列に変換する(uint virtualKey, string expected)
        {
            Assert.Equal(expected, HotKeyDisplay.KeyText(virtualKey));
        }
    }
}
