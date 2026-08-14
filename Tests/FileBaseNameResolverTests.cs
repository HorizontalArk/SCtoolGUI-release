using SCtoolGui;

namespace SCtoolGui.Tests
{
    public class FileBaseNameResolverTests
    {
        [Fact]
        public void OFFなら登録名をそのまま使う()
        {
            string result = FileBaseNameResolver.Resolve(
                useWindowTitle: false, windowTitle: "実際のタイトル", registeredName: "MyGame");
            Assert.Equal("MyGame", result);
        }

        [Fact]
        public void ONならウィンドウタイトルを安全化して使う()
        {
            string result = FileBaseNameResolver.Resolve(
                useWindowTitle: true, windowTitle: "a/b:c", registeredName: "MyGame");
            Assert.Equal("a_b_c", result);
        }

        [Fact]
        public void ONでもタイトルが空なら登録名にフォールバックする()
        {
            string result = FileBaseNameResolver.Resolve(
                useWindowTitle: true, windowTitle: "", registeredName: "MyGame");
            Assert.Equal("MyGame", result);
        }

        [Fact]
        public void ONでタイトルが長すぎる場合は切り詰められる()
        {
            string result = FileBaseNameResolver.Resolve(
                useWindowTitle: true, windowTitle: new string('x', 200), registeredName: "MyGame");
            Assert.True(result.Length <= 80);
        }
    }
}
