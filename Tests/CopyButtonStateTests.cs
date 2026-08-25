using SCtoolGui;
using Xunit;

namespace SCtoolGui.Tests
{
    public class CopyButtonStateTests
    {
        // 既定対象が LastSaved のときは、保存画像の有無でコピーボタン本体の有効/無効が決まる。
        [Fact]
        public void 既定LastSavedかつ保存画像なしは無効()
        {
            Assert.False(CopyButtonState.IsMainCopyEnabled(CopyTarget.LastSaved, hasLastCapture: false));
        }

        [Fact]
        public void 既定LastSavedかつ保存画像ありは有効()
        {
            Assert.True(CopyButtonState.IsMainCopyEnabled(CopyTarget.LastSaved, hasLastCapture: true));
        }

        // プレビュー系（TempPreview / FreshPreview）は対象ウィンドウがあれば動くため、
        // 保存画像が無くても本体ボタンは有効にする。
        [Theory]
        [InlineData(CopyTarget.TempPreview)]
        [InlineData(CopyTarget.FreshPreview)]
        public void 既定プレビュー系は保存画像なしでも有効(CopyTarget target)
        {
            Assert.True(CopyButtonState.IsMainCopyEnabled(target, hasLastCapture: false));
        }

        // 1枚も撮っていない状態で既定対象を LastSaved から FreshPreview に変えると、
        // 無効→有効へ切り替わる（設定変更が反映される）ことを保証する。
        [Fact]
        public void 保存画像なしで既定をLastSavedからFreshPreviewへ変えると無効から有効になる()
        {
            bool before = CopyButtonState.IsMainCopyEnabled(CopyTarget.LastSaved, hasLastCapture: false);
            bool after = CopyButtonState.IsMainCopyEnabled(CopyTarget.FreshPreview, hasLastCapture: false);

            Assert.False(before);
            Assert.True(after);
        }
    }
}
