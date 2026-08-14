using SCtoolGui;

namespace SCtoolGui.Tests
{
    public class CopyTargetResolverTests
    {
        [Fact]
        public void 一時プレビュー指定で存在すればそのパスを返す()
        {
            string? result = CopyTargetResolver.Resolve(
                CopyTarget.TempPreview,
                tempPreviewPath: "temp.jpg", tempPreviewExists: true,
                lastSavedPath: "saved.jpg", lastSavedExists: true);
            Assert.Equal("temp.jpg", result);
        }

        [Fact]
        public void 最後に保存した画像指定で存在すればそのパスを返す()
        {
            string? result = CopyTargetResolver.Resolve(
                CopyTarget.LastSaved,
                tempPreviewPath: "temp.jpg", tempPreviewExists: true,
                lastSavedPath: "saved.jpg", lastSavedExists: true);
            Assert.Equal("saved.jpg", result);
        }

        [Fact]
        public void 一時プレビュー指定でも存在しなければnullを返す()
        {
            string? result = CopyTargetResolver.Resolve(
                CopyTarget.TempPreview,
                tempPreviewPath: "temp.jpg", tempPreviewExists: false,
                lastSavedPath: "saved.jpg", lastSavedExists: true);
            Assert.Null(result);
        }

        [Fact]
        public void 最後に保存した画像指定でも存在しなければnullを返す()
        {
            string? result = CopyTargetResolver.Resolve(
                CopyTarget.LastSaved,
                tempPreviewPath: "temp.jpg", tempPreviewExists: true,
                lastSavedPath: "saved.jpg", lastSavedExists: false);
            Assert.Null(result);
        }

        [Fact]
        public void Parseは設定文字列をenumに変換する()
        {
            Assert.Equal(CopyTarget.TempPreview, CopyTargetResolver.Parse("TempPreview"));
            Assert.Equal(CopyTarget.LastSaved, CopyTargetResolver.Parse("LastSaved"));
        }

        [Fact]
        public void Parseは未知の値をLastSavedにフォールバックする()
        {
            Assert.Equal(CopyTarget.LastSaved, CopyTargetResolver.Parse("なにか"));
        }
    }
}
