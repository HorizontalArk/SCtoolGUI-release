using SCtoolGui;

namespace SCtoolGui.Tests
{
    public class PreviewOrientationLogicTests
    {
        [Theory]
        [InlineData(1920, 1080, PreviewMode.Horizontal)]
        [InlineData(1080, 1920, PreviewMode.Vertical)]
        [InlineData(500, 500, PreviewMode.Horizontal)] // 正方形は横扱い
        public void 画像の向き判定(double w, double h, PreviewMode expected)
        {
            Assert.Equal(expected, PreviewOrientationLogic.DetectImageOrientation(w, h));
        }

        [Fact]
        public void Off設定は常にNone()
        {
            Assert.Equal(AutoSwitchDecision.None,
                PreviewOrientationLogic.Decide("Off", PreviewMode.Horizontal, PreviewMode.Vertical));
        }

        [Fact]
        public void 一致していればNone()
        {
            Assert.Equal(AutoSwitchDecision.None,
                PreviewOrientationLogic.Decide("Force", PreviewMode.Vertical, PreviewMode.Vertical));
        }

        [Fact]
        public void Force不一致はSwitch()
        {
            Assert.Equal(AutoSwitchDecision.Switch,
                PreviewOrientationLogic.Decide("Force", PreviewMode.Horizontal, PreviewMode.Vertical));
        }

        [Fact]
        public void Prompt不一致はPrompt()
        {
            Assert.Equal(AutoSwitchDecision.Prompt,
                PreviewOrientationLogic.Decide("Prompt", PreviewMode.Horizontal, PreviewMode.Vertical));
        }
    }
}
