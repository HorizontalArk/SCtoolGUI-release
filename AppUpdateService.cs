using System;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace SCtoolGui
{
    /// <summary>Velopack による自動更新のラッパー。</summary>
    public class AppUpdateService
    {
        private const string ReleasesRepoUrl = "https://github.com/HorizontalArk/SCtoolGUI-release";
        // 旧 git 版の SCtoolGui.UpdateManager と名前が衝突するため Velopack 側を明示修飾する。
        private readonly Velopack.UpdateManager _mgr;

        public AppUpdateService()
        {
            // prerelease は取り込まない（第3引数 false）。公開repoなのでトークン不要（null）。
            _mgr = new Velopack.UpdateManager(new GithubSource(ReleasesRepoUrl, null, false));
        }

        /// <summary>Velopack でインストールされた状態か。dev 実行時は false。</summary>
        public bool IsInstalled => _mgr.IsInstalled;

        /// <summary>現在のバージョン文字列。未インストール時は null。</summary>
        public string? CurrentVersion => _mgr.CurrentVersion?.ToString();

        /// <summary>更新があれば UpdateInfo を返す。無ければ null。</summary>
        public Task<UpdateInfo?> CheckAsync() => _mgr.CheckForUpdatesAsync();

        /// <summary>
        /// 更新をDLして適用し、アプリを再起動する。成功時はこの呼び出しからは戻らない。
        /// <paramref name="onProgress"/> にはDLの進捗(0〜100)が渡る。
        /// </summary>
        public async Task DownloadAndApplyAsync(UpdateInfo info, Action<int>? onProgress = null)
        {
            await _mgr.DownloadUpdatesAsync(info, onProgress);
            _mgr.ApplyUpdatesAndRestart(info);
        }
    }
}
