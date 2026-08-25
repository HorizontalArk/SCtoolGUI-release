using System.IO;
using SCtoolGui;
using Xunit;

namespace SCtoolGui.Tests
{
    public class DeveloperKeyPathTests
    {
        [Fact]
        public void developer_keyはSCtoolGui配下に解決される()
        {
            string p = SettingsManager.ResolveDeveloperKeyPath(@"C:\Users\x\AppData\Roaming");
            Assert.Equal(
                Path.Combine(@"C:\Users\x\AppData\Roaming", "SCtoolGui", "developer.key"),
                p);
        }

        [Fact]
        public void AppSettingsの開発者系既定はfalse()
        {
            var s = new AppSettings();
            Assert.False(s.DeveloperModeEnabled);
            Assert.False(s.IncludePrereleases);
        }
    }
}
