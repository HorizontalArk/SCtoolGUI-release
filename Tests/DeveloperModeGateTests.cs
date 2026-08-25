using SCtoolGui;
using Xunit;

namespace SCtoolGui.Tests
{
    public class DeveloperModeGateTests
    {
        // DeveloperModeGate の ExpectedHashHex と同一の合言葉を使う。
        private const string Passphrase = "SCtoolGui-dev-3f9c1a7e5b2d48e6a0c9f14b7d8e2a65";

        [Fact]
        public void 正しい合言葉で解錠される()
        {
            Assert.True(DeveloperModeGate.IsUnlocked(Passphrase));
        }

        [Fact]
        public void 前後の空白は無視して解錠される()
        {
            Assert.True(DeveloperModeGate.IsUnlocked("  " + Passphrase + "\r\n"));
        }

        [Fact]
        public void 誤った合言葉では解錠されない()
        {
            Assert.False(DeveloperModeGate.IsUnlocked("wrong"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void 空やnullは解錠されない(string? content)
        {
            Assert.False(DeveloperModeGate.IsUnlocked(content));
        }

        [Fact]
        public void Sha256Hexは既知値と一致する()
        {
            // "abc" の SHA256 は既知の固定値。
            Assert.Equal(
                "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
                DeveloperModeGate.Sha256Hex("abc"));
        }
    }
}
