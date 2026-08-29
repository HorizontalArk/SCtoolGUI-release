using SCtoolGui;

namespace SCtoolGui.Tests
{
    public class CaptureCropCalculatorTests
    {
        [Fact]
        public void システム枠分だけ縮小したサイズになる()
        {
            var r = CaptureCropCalculator.Compute(left: 0, top: 0, right: 802, bottom: 602, topCutPixels: 0);
            Assert.Equal(800, r.CaptureWidth);
            Assert.Equal(600, r.CaptureHeight);
            Assert.Equal(600, r.FinalHeight);
            Assert.True(r.IsValid);
        }

        [Fact]
        public void topCutPixels分だけFinalHeightがさらに縮む()
        {
            var r = CaptureCropCalculator.Compute(left: 0, top: 0, right: 802, bottom: 602, topCutPixels: 50);
            Assert.Equal(600, r.CaptureHeight);
            Assert.Equal(550, r.FinalHeight);
            Assert.True(r.IsValid);
        }

        [Fact]
        public void カット後の高さが0以下なら無効()
        {
            var r = CaptureCropCalculator.Compute(left: 0, top: 0, right: 802, bottom: 602, topCutPixels: 600);
            Assert.False(r.IsValid);
        }

        [Fact]
        public void 幅がシステム枠以下なら無効()
        {
            var r = CaptureCropCalculator.Compute(left: 0, top: 0, right: 1, bottom: 602, topCutPixels: 0);
            Assert.False(r.IsValid);
        }
    }
}
