using System.Collections.Generic;
using System.Threading.Tasks;
using SCtoolGui;
using Xunit;

namespace SCtoolGui.Tests
{
    public class UpdateFlowTests
    {
        /// <summary>
        /// 更新再起動は WPF の終了フロー(OnClosed)を経由しないため、DL/適用の前に
        /// 必ず終了処理(cleanup=位置・設定の保存)が走ることを保証する。
        /// ここでは呼び出し順を記録し、cleanup がダウンロードより先であることを検証する。
        /// </summary>
        [Fact]
        public async Task RunAsyncはcleanupをダウンロードより先に実行する()
        {
            var order = new List<string>();

            await UpdateFlow.RunAsync(
                cleanup: () => order.Add("cleanup"),
                downloadAndApply: () => { order.Add("download"); return Task.CompletedTask; });

            Assert.Equal(new[] { "cleanup", "download" }, order);
        }

        /// <summary>
        /// cleanup が例外を投げた場合、保存できないまま再起動へ進まない（安全側）。
        /// ダウンロードは呼ばれない。
        /// </summary>
        [Fact]
        public async Task cleanupが失敗したらダウンロードへ進まない()
        {
            bool downloaded = false;

            await Assert.ThrowsAsync<System.InvalidOperationException>(async () =>
            {
                await UpdateFlow.RunAsync(
                    cleanup: () => throw new System.InvalidOperationException("保存失敗"),
                    downloadAndApply: () => { downloaded = true; return Task.CompletedTask; });
            });

            Assert.False(downloaded);
        }
    }
}
