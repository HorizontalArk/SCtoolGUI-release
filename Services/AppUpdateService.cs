using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace SCtoolGui
{
    /// <summary>Velopack による自動更新のラッパー。</summary>
    public class AppUpdateService
    {
        /// <summary>リリース公開先のリポジトリURL。GitHub Releaseページのリンク組み立てにも使う。</summary>
        public const string ReleasesRepoUrl = "https://github.com/HorizontalArk/SCtoolGUI-release";
        // 旧 git 版の SCtoolGui.UpdateManager と名前が衝突するため Velopack 側を明示修飾する。
        private readonly Velopack.UpdateManager _mgr;
        private readonly GithubSource _source;

        /// <param name="includePrereleases">
        /// true なら prerelease も更新対象・一覧対象にする（開発者モード用）。既定 false。
        /// </param>
        public AppUpdateService(bool includePrereleases = false)
        {
            // 公開repoなのでトークン不要（null）。prerelease 取り込みは設定で切り替える。
            _source = new GithubSource(ReleasesRepoUrl, null, includePrereleases);
            // 任意バージョンへの変更（ダウングレード）を許可する。通常更新には影響しない。
            _mgr = new Velopack.UpdateManager(_source, new UpdateOptions { AllowVersionDowngrade = true });
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
        public Task DownloadAndApplyAsync(UpdateInfo info, Action<int>? onProgress = null)
            => DownloadAndApply(info, onProgress);

        /// <summary>
        /// 利用可能なリリース一覧（prerelease 取り込み設定に従う）を返す。取得失敗時は空。
        /// バージョン変更（更新/ダウングレード）の選択肢として使う。
        /// </summary>
        public async Task<IReadOnlyList<VelopackAsset>> GetAvailableReleasesAsync()
        {
            try
            {
                // channel=null で OS 既定チャンネル。stagingId=null、latestLocalRelease=null。
                var feed = await _source.GetReleaseFeed(
                    logger: null!, appId: null!, channel: null!, stagingId: null, latestLocalRelease: null!);
                return (IReadOnlyList<VelopackAsset>?)feed?.Assets ?? Array.Empty<VelopackAsset>();
            }
            catch
            {
                return Array.Empty<VelopackAsset>();
            }
        }

        /// <summary>
        /// 指定した版へ更新/ダウングレードして再起動する。成功時はこの呼び出しからは戻らない。
        /// ダウングレードは UpdateManager の AllowVersionDowngrade で許可済み。
        /// </summary>
        public Task DownloadAndApplyAssetAsync(VelopackAsset asset, Action<int>? onProgress = null)
        {
            // 対象が現在より低ければダウングレード。UpdateInfo(target, isDowngrade, baseRelease, deltas)。
            bool isDowngrade = _mgr.CurrentVersion != null
                && asset.Version != null
                && asset.Version < _mgr.CurrentVersion;
            var info = new UpdateInfo(asset, isDowngrade, null!, Array.Empty<VelopackAsset>());
            return DownloadAndApply(info, onProgress);
        }

        /// <summary>DL・適用の実処理。上の2つの公開メソッドで共有する。</summary>
        private async Task DownloadAndApply(UpdateInfo info, Action<int>? onProgress)
        {
            await _mgr.DownloadUpdatesAsync(info, onProgress);
            _mgr.ApplyUpdatesAndRestart(info);
        }
    }
}
