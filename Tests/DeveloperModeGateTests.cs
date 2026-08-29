using SCtoolGui;
using Xunit;

namespace SCtoolGui.Tests
{
    public class DeveloperModeGateTests
    {
        // テスト専用の合言葉。本番の合言葉(DeveloperModeGate.ExpectedHashHexの元)とは無関係で、
        // このリポジトリには本番の合言葉を一切書かない。
        private const string TestPassphrase = "test-only-passphrase-unrelated-to-production";
        private static readonly string TestHash = DeveloperModeGate.Sha256Hex(TestPassphrase);

        [Fact]
        public void 正しい合言葉で解錠される()
        {
            Assert.True(DeveloperModeGate.IsUnlocked(TestPassphrase, TestHash));
        }

        [Fact]
        public void 前後の空白は無視して解錠される()
        {
            Assert.True(DeveloperModeGate.IsUnlocked("  " + TestPassphrase + "\r\n", TestHash));
        }

        [Fact]
        public void 誤った合言葉では解錠されない()
        {
            Assert.False(DeveloperModeGate.IsUnlocked("wrong", TestHash));
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

        [Theory]
        [InlineData(true, true, true)]
        [InlineData(true, false, false)]
        [InlineData(false, true, false)]
        [InlineData(false, false, false)]
        public void 開発者タブは解錠済みかつトグルONのときだけ表示する(bool unlocked, bool toggleOn, bool expected)
        {
            Assert.Equal(expected, DeveloperModeGate.ShouldShowDeveloperTab(unlocked, toggleOn));
        }
    }
}
