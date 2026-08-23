using SCtoolGui;
using Xunit;

namespace SCtoolGui.Tests
{
    public class WindowPlacementLogicTests
    {
        [Fact]
        public void Resolve_Normal_UsesNormalLeftTop_NotMaximized()
        {
            var r = WindowPlacementLogic.Resolve(
                isMaximized: false,
                normalLeft: 100, normalTop: 200,
                restoreLeft: 10, restoreTop: 20);

            Assert.Equal(100, r.left);
            Assert.Equal(200, r.top);
            Assert.False(r.maximized);
        }

        [Fact]
        public void Resolve_Maximized_UsesRestoreBounds_Maximized()
        {
            var r = WindowPlacementLogic.Resolve(
                isMaximized: true,
                normalLeft: 100, normalTop: 200,
                restoreLeft: 10, restoreTop: 20);

            Assert.Equal(10, r.left);
            Assert.Equal(20, r.top);
            Assert.True(r.maximized);
        }
    }
}
