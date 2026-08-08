using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace SCtoolGui
{
    /// <summary>アップデート判定の結果と、その理由。</summary>
    public readonly struct UpdateStatus
    {
        public UpdateStatus(bool hasUpdate, string reason)
        {
            HasUpdate = hasUpdate;
            Reason = reason;
        }

        public bool HasUpdate { get; }

        /// <summary>判定理由。通知を出さなかった場合にログへ出すために持つ。</summary>
        public string Reason { get; }
    }

    public static class UpdateManager
    {
        /// <summary>更新の取得元ブランチ。</summary>
        private const string ReleaseBranch = "main";
        private const string ReleaseRef = "origin/" + ReleaseBranch;

        /// <summary>gitコマンドの実行結果。</summary>
        private readonly struct GitResult
        {
            public GitResult(int exitCode, string output)
            {
                ExitCode = exitCode;
                Output = output;
            }

            public int ExitCode { get; }
            public string Output { get; }
            public bool Succeeded => ExitCode == 0;
        }

        /// <summary>アップデートの有無を非同期でチェックする。</summary>
        public static async Task<bool> CheckForUpdatesAsync()
        {
            var status = await GetUpdateStatusAsync();
            return status.HasUpdate;
        }

        /// <summary>
        /// アップデートの有無を、理由つきで判定する。
        ///
        /// 「pullできるか」ではなく「リリースブランチの先端が自分の履歴に含まれているか」で判定する。
        /// 前者だと、独自ブランチで作業中にmainが進んだだけで更新ありと誤検知し、
        /// 作業中のブランチへmainをマージしにいってしまうため。
        /// </summary>
        public static async Task<UpdateStatus> GetUpdateStatusAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    // リリースブランチ以外での作業中は、更新を促さない。
                    // 通知に従うと開発中の変更へmainをマージすることになるため。
                    var branch = RunGit("rev-parse --abbrev-ref HEAD");
                    if (!branch.Succeeded) return new UpdateStatus(false, "gitリポジトリではありません。");

                    string current = branch.Output.Trim();
                    if (current != ReleaseBranch)
                    {
                        return new UpdateStatus(false, $"{current} ブランチで作業中のため、更新確認をスキップしました。");
                    }

                    if (!RunGit("fetch").Succeeded)
                    {
                        return new UpdateStatus(false, "リモートへの接続に失敗したため、更新を確認できませんでした。");
                    }

                    // origin/main の先端が自分の履歴に含まれていれば最新。
                    // 含まれていなければ古い（＝更新あり）。
                    var ancestor = RunGit($"merge-base --is-ancestor {ReleaseRef} HEAD");

                    // 終了コード 0=祖先(最新) / 1=祖先でない(古い) / それ以外はエラー
                    if (ancestor.ExitCode == 0) return new UpdateStatus(false, "アプリケーションは最新です。");
                    if (ancestor.ExitCode != 1)
                    {
                        return new UpdateStatus(false, $"{ReleaseRef} を参照できませんでした。");
                    }

                    var behind = RunGit($"rev-list --count HEAD..{ReleaseRef}");
                    string count = behind.Succeeded ? behind.Output.Trim() : "";

                    return new UpdateStatus(true, string.IsNullOrEmpty(count)
                        ? "新しいアップデートがあります。"
                        : $"新しいアップデートがあります（{count} 件の更新）。");
                }
                catch
                {
                    // Gitが入っていない場合など
                    return new UpdateStatus(false, "更新の確認に失敗しました。");
                }
            });
        }

        /// <summary>gitコマンドを実行し、終了コードと標準出力を返す。</summary>
        private static GitResult RunGit(string arguments)
        {
            var info = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = FindProjectDirectory(),
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using var process = Process.Start(info);
            if (process == null) return new GitResult(-1, "");

            string output = process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit();

            return new GitResult(process.ExitCode, output);
        }

        /// <summary>.csproj のあるディレクトリ（プロジェクトルート）を探す。見つからなければ実行ディレクトリ。</summary>
        private static string FindProjectDirectory()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            string? searchDir = baseDir;
            while (searchDir != null && !Directory.GetFiles(searchDir, "*.csproj").Any())
            {
                searchDir = Directory.GetParent(searchDir)?.FullName;
            }

            return searchDir ?? baseDir;
        }

        /// <summary>
        /// 更新を実行してよいかを検証する。実行できない場合は理由を返す。
        /// pull は作業ツリーを書き換えるため、失われて困る変更が無いことを先に確かめる。
        /// </summary>
        public static string? ValidateCanUpdate()
        {
            try
            {
                var branch = RunGit("rev-parse --abbrev-ref HEAD");
                if (!branch.Succeeded) return "gitリポジトリではないため、更新できません。";

                string current = branch.Output.Trim();
                if (current != ReleaseBranch)
                {
                    return $"現在 {current} ブランチにいます。\n" +
                           $"更新すると作業中の変更に {ReleaseBranch} がマージされてしまうため、中止しました。";
                }

                // 未コミットの変更があると pull が失敗する、または変更を巻き込む
                var status = RunGit("status --porcelain");
                if (status.Succeeded && !string.IsNullOrWhiteSpace(status.Output))
                {
                    return "コミットされていない変更があります。\n" +
                           "更新すると失われる可能性があるため、中止しました。";
                }

                return null;
            }
            catch
            {
                return "更新可否の確認に失敗しました。";
            }
        }

        // バッチファイルを生成してアプリを更新・ビルド・再起動する
        public static void ExecuteUpdateAndRestart()
        {
            string currentExePath = Process.GetCurrentProcess().MainModule?.FileName ?? "SCtoolGui.exe";
            string projectDir = FindProjectDirectory();

            string updaterBatchPath = Path.Combine(projectDir, "auto_restart_updater.bat");

            string logPath = Path.Combine(projectDir, "update_log.txt");

            // pull や build が失敗した場合は、そのまま起動せずログを残して知らせる。
            // 画面を隠して実行しているため、失敗が見えないまま古い実行ファイルが
            // 起動してしまうのを防ぐ。
            string batchContent = $@"@echo off
chcp 65001 > nul
cd /d ""{projectDir}""

timeout /t 2 /nobreak > nul

git pull --ff-only origin {ReleaseBranch} > ""{logPath}"" 2>&1
if errorlevel 1 (
    echo. >> ""{logPath}""
    echo [SCtool] 更新の取得に失敗したため、更新を中止しました。 >> ""{logPath}""
    start """" notepad.exe ""{logPath}""
    start """" ""{currentExePath}""
    exit /b 1
)

dotnet build >> ""{logPath}"" 2>&1
if errorlevel 1 (
    echo. >> ""{logPath}""
    echo [SCtool] ビルドに失敗しました。 >> ""{logPath}""
    start """" notepad.exe ""{logPath}""
    exit /b 1
)

start """" ""{currentExePath}""
exit
";
            File.WriteAllText(updaterBatchPath, batchContent, new System.Text.UTF8Encoding(false));

            // コマンドプロンプトを完全に非表示にして裏で実行する設定
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{updaterBatchPath}\"",
                WorkingDirectory = projectDir,
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            Process.Start(startInfo);

            Application.Current.Shutdown();
        }
    }
}