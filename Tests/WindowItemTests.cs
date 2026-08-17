using System.ComponentModel;
using SCtoolGui;

namespace SCtoolGui.Tests
{
    public class WindowItemTests
    {
        [Fact]
        public void Title変更でPropertyChangedが発火する()
        {
            var item = new WindowItem { Title = "旧タイトル" };
            string? changed = null;
            item.PropertyChanged += (s, e) => changed = e.PropertyName;

            item.Title = "新タイトル";

            Assert.Equal(nameof(WindowItem.Title), changed);
        }

        [Fact]
        public void 同じ値の代入では通知しない()
        {
            var item = new WindowItem { Title = "同じ" };
            bool fired = false;
            item.PropertyChanged += (s, e) => fired = true;

            item.Title = "同じ";

            Assert.False(fired);
        }
    }
}
