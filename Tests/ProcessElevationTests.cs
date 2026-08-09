using System.Diagnostics;
using System.Security.Principal;
using SCtoolGui;

namespace SCtoolGui.Tests
{
    public class ProcessElevationTests
    {
        /// <summary>
        /// 自分自身のプロセスに対する昇格判定が、OS が報告する実際の昇格状態
        /// （WindowsPrincipal）と一致すること。OpenProcessToken+GetTokenInformation
        /// の連鎖が正しく動くことを、既知の真値に対して検証する。
        /// </summary>
        [Fact]
        public void 自プロセスの昇格判定は実際の昇格状態と一致する()
        {
            uint pid = (uint)Process.GetCurrentProcess().Id;
            bool expected = new WindowsPrincipal(WindowsIdentity.GetCurrent())
                .IsInRole(WindowsBuiltInRole.Administrator);

            bool actual = ProcessElevation.IsProcessElevated(pid);

            Assert.Equal(expected, actual);
        }
    }
}
