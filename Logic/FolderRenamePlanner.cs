using System;
using System.IO;

namespace SCtoolGui
{
    public enum FolderRenameAction { NoMove, Move, ConflictSkip }

    public record FolderRenamePlan(FolderRenameAction Action, string? OldPath, string? NewPath);

    /// <summary>
    /// 名前変更（呼び名変更）時に、旧名の画像フォルダを新名へ移動すべきか判定する純粋ロジック。
    /// アプリごとフォルダ分けが有効で、旧フォルダが在り、新フォルダが未作成のときだけ移動する。
    /// </summary>
    public static class FolderRenamePlanner
    {
        public static FolderRenamePlan Plan(
            string baseDir, string oldName, string newName, bool perAppFolder, Func<string, bool> exists)
        {
            if (!perAppFolder)
                return new FolderRenamePlan(FolderRenameAction.NoMove, null, null);

            string oldSafe = FileNameUtil.ToSafeName(oldName);
            string newSafe = FileNameUtil.ToSafeName(newName);
            if (string.Equals(oldSafe, newSafe, StringComparison.OrdinalIgnoreCase))
                return new FolderRenamePlan(FolderRenameAction.NoMove, null, null);

            string oldPath = Path.Combine(baseDir, oldSafe);
            string newPath = Path.Combine(baseDir, newSafe);

            if (!exists(oldPath))
                return new FolderRenamePlan(FolderRenameAction.NoMove, null, null);
            if (exists(newPath))
                return new FolderRenamePlan(FolderRenameAction.ConflictSkip, oldPath, newPath);

            return new FolderRenamePlan(FolderRenameAction.Move, oldPath, newPath);
        }
    }
}
