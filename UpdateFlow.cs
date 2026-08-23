using System;
using System.Threading.Tasks;

namespace SCtoolGui
{
    /// <summary>
    /// アップデート適用の実行順序を司る調整ロジック。
    ///
    /// Velopack の再起動は WPF の終了フロー(OnClosed)を経由せずプロセスを即終了するため、
    /// ダウンロード・適用の前に終了処理(位置・設定の保存など)を必ず済ませておく必要がある。
    /// この順序契約をここに固定し、UI(WPF)非依存にしてテスト可能にする。
    /// </summary>
    public static class UpdateFlow
    {
        /// <summary>
        /// <paramref name="cleanup"/>(終了処理=保存)を先に実行し、その後
        /// <paramref name="downloadAndApply"/>(DL・適用・再起動)を実行する。
        /// cleanup が例外を投げた場合はダウンロードへ進まない(保存できないまま再起動しない安全側)。
        /// </summary>
        public static async Task RunAsync(Action cleanup, Func<Task> downloadAndApply)
        {
            cleanup();
            await downloadAndApply();
        }
    }
}
