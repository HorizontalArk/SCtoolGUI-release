using SCtoolGui;

namespace SCtoolGui.Tests
{
    public class ShortcutLocationResolverTests
    {
        [Fact]
        public void 両方ON()
        {
            var r = ShortcutLocationResolver.Resolve(desktop: true, startMenu: true);
            Assert.Equal(ShortcutChoice.Desktop | ShortcutChoice.StartMenu, r);
            Assert.True(ShortcutLocationResolver.HasAny(r));
        }

        [Fact]
        public void デスクトップのみ()
        {
            var r = ShortcutLocationResolver.Resolve(desktop: true, startMenu: false);
            Assert.Equal(ShortcutChoice.Desktop, r);
        }

        [Fact]
        public void 両方OFFはNone()
        {
            var r = ShortcutLocationResolver.Resolve(desktop: false, startMenu: false);
            Assert.Equal(ShortcutChoice.None, r);
            Assert.False(ShortcutLocationResolver.HasAny(r));
        }
    }
}
