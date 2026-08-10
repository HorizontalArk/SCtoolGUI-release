using System;
using System.Collections.Generic;
using System.IO;
using SCtoolGui;

namespace SCtoolGui.Tests
{
    public class FolderRenamePlannerTests
    {
        private static Func<string,bool> ExistsIn(params string[] paths)
        {
            var set = new HashSet<string>(paths, StringComparer.OrdinalIgnoreCase);
            return p => set.Contains(p);
        }

        [Fact]
        public void フォルダ分けOFFなら移動しない()
        {
            var plan = FolderRenamePlanner.Plan(@"C:\base", "旧", "新", perAppFolder: false, ExistsIn());
            Assert.Equal(FolderRenameAction.NoMove, plan.Action);
        }

        [Fact]
        public void 旧フォルダが無ければ移動しない()
        {
            var plan = FolderRenamePlanner.Plan(@"C:\base", "旧", "新", perAppFolder: true, ExistsIn());
            Assert.Equal(FolderRenameAction.NoMove, plan.Action);
        }

        [Fact]
        public void 旧のみ存在すれば移動()
        {
            string old = Path.Combine(@"C:\base", FileNameUtil.ToSafeName("旧"));
            var plan = FolderRenamePlanner.Plan(@"C:\base", "旧", "新", perAppFolder: true, ExistsIn(old));
            Assert.Equal(FolderRenameAction.Move, plan.Action);
            Assert.Equal(old, plan.OldPath);
            Assert.Equal(Path.Combine(@"C:\base", FileNameUtil.ToSafeName("新")), plan.NewPath);
        }

        [Fact]
        public void 新も存在すれば衝突スキップ()
        {
            string old = Path.Combine(@"C:\base", FileNameUtil.ToSafeName("旧"));
            string neu = Path.Combine(@"C:\base", FileNameUtil.ToSafeName("新"));
            var plan = FolderRenamePlanner.Plan(@"C:\base", "旧", "新", perAppFolder: true, ExistsIn(old, neu));
            Assert.Equal(FolderRenameAction.ConflictSkip, plan.Action);
        }

        [Fact]
        public void 安全名が同一なら移動しない()
        {
            string old = Path.Combine(@"C:\base", FileNameUtil.ToSafeName("a/b"));
            // "a/b" と "a:b" は安全名が同じ "a_b"
            var plan = FolderRenamePlanner.Plan(@"C:\base", "a/b", "a:b", perAppFolder: true, ExistsIn(old));
            Assert.Equal(FolderRenameAction.NoMove, plan.Action);
        }
    }
}
