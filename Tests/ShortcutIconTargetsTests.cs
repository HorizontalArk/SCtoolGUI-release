using System;
using System.Linq;
using SCtoolGui;

namespace SCtoolGui.Tests
{
    public class ShortcutIconTargetsTests
    {
        [Fact]
        public void 既知の3種のlnkパスを返す()
        {
            var paths = ShortcutIconTargets.Resolve(
                appData: @"C:\Users\u\AppData\Roaming",
                userProfile: @"C:\Users\u");

            Assert.Contains(paths, p => p.EndsWith(@"Start Menu\Programs\SCtoolGui.lnk"));
            Assert.Contains(paths, p => p.EndsWith(@"Desktop\SCtoolGui.lnk"));
            Assert.Contains(paths, p => p.EndsWith(@"User Pinned\TaskBar\SCtoolGui.lnk"));
            Assert.Equal(3, paths.Count);
        }
    }
}
