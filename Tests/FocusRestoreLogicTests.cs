using SCtoolGui;
using Xunit;

namespace SCtoolGui.Tests
{
    public class FocusRestoreLogicTests
    {
        [Theory]
        [InlineData(true, true)]
        [InlineData(false, false)]
        public void AfterCapture_FollowsCaptureSetting(bool onCapture, bool expected)
        {
            Assert.Equal(expected, FocusRestoreLogic.AfterCapture(onCapture));
        }

        // 統一ON: プレビューはキャプチャ側の値に追従する（プレビュー個別値は無視）
        [Theory]
        [InlineData(true, true, false, true)]   // unify, capture=true, preview=false → true
        [InlineData(true, false, true, false)]  // unify, capture=false, preview=true → false
        public void AfterPreview_Unified_FollowsCapture(bool unify, bool onCapture, bool onPreview, bool expected)
        {
            Assert.Equal(expected, FocusRestoreLogic.AfterPreview(unify, onCapture, onPreview));
        }

        // 統一OFF: プレビューは個別値に従う
        [Theory]
        [InlineData(false, true, false, false)] // not unify, preview=false → false
        [InlineData(false, false, true, true)]  // not unify, preview=true → true
        public void AfterPreview_NotUnified_FollowsPreview(bool unify, bool onCapture, bool onPreview, bool expected)
        {
            Assert.Equal(expected, FocusRestoreLogic.AfterPreview(unify, onCapture, onPreview));
        }
    }
}
