using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SCtoolGui
{
    public class AppSettings
    {
        /// <summary>
        /// 旧形式（タイトル→カット量）。Targets へ移行済みで、読み込み時の互換のためだけに残している。
        /// </summary>
        public Dictionary<string, int> CutSettings { get; set; } = new();

        /// <summary>キャプチャ対象アプリの一覧。実行ファイルパスで識別する。</summary>
        public List<TargetInfo> Targets { get; set; } = new();

        /// <summary>最後に選択していたターゲットの識別子（exeパス、旧データではタイトル）。</summary>
        public string LastSelectedTargetKey { get; set; } = "";

        public string SaveDirectory { get; set; } = "";
        public uint HotkeyModifiers { get; set; } = 0x0006;
        public uint HotkeyKey { get; set; } = 0x53;

        /// <summary>旧形式の最終選択ウィンドウ名。LastSelectedTargetKey へ移行済み。</summary>
        public string LastSelectedWindow { get; set; } = "";
        public double? WindowLeft { get; set; } = null;
        public double? WindowTop { get; set; } = null;
        
        public bool AppTopmost { get; set; } = false;
        public bool SaveInWindowNameFolder { get; set; } = false;
        public bool ResetSettingsOnWindowChange { get; set; } = true;
        public bool AutoCopyClipboard { get; set; } = true;
        
        public bool PlayShutterSound { get; set; } = true;
        // ★追加: シャッター音量 (0.0 ～ 1.0)
        public double ShutterVolume { get; set; } = 1.0;

        /// <summary>常に管理者として起動するか。管理者権限のアプリを常用する人向け。</summary>
        public bool AlwaysRunAsAdmin { get; set; } = false;

        /// <summary>UIテーマ。"System"（OS追従）/ "Light" / "Dark"。</summary>
        public string Theme { get; set; } = "System";

        /// <summary>ユーザー指定のウィンドウアイコン画像パス。空なら埋め込み既定を使う。</summary>
        public string IconPath { get; set; } = "";

        /// <summary>初回セットアップウィザードを完了したか。false の間は初回起動時に表示する。</summary>
        public bool SetupCompleted { get; set; } = false;

        /// <summary>プレビューの向き。"Horizontal"（下部・全幅）/ "Vertical"（片側・縦長）。</summary>
        public string PreviewOrientation { get; set; } = "Horizontal";

        /// <summary>縦モード時にプレビューを置く側。"Right" / "Left"。</summary>
        public string VerticalPreviewSide { get; set; } = "Right";

        /// <summary>縦横比による自動切替の動作。"Off" / "Prompt"（確認）/ "Force"（強制）。</summary>
        public string PreviewAutoSwitch { get; set; } = "Prompt";

        /// <summary>横モード時に記憶する窓サイズ（null なら既定サイズ）。</summary>
        public double? HorizontalWindowWidth { get; set; } = null;
        public double? HorizontalWindowHeight { get; set; } = null;

        /// <summary>縦モード時に記憶する窓サイズ（null なら既定サイズ）。</summary>
        public double? VerticalWindowWidth { get; set; } = null;
        public double? VerticalWindowHeight { get; set; } = null;

        /// <summary>縦モード時の操作群カラム幅（スプリッターのドラッグ結果。null なら既定幅）。</summary>
        public double? VerticalOperationsWidth { get; set; } = null;
    }

    public class SettingsManager
    {
        private readonly string _settingsFile = ResolveSettingsFile(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppDomain.CurrentDomain.BaseDirectory);

        /// <summary>
        /// 設定ファイルの保存先を決める。Velopackはバージョンごとに別フォルダへ
        /// インストールするため、exe隣ではなく %AppData%\SCtoolGui に置く。
        /// 旧位置（exe隣）に設定が在れば一度だけ移行する。
        /// </summary>
        public static string ResolveSettingsFile(string appDataRoot, string legacyDir)
        {
            string dir = Path.Combine(appDataRoot, "SCtoolGui");
            Directory.CreateDirectory(dir);
            string newPath = Path.Combine(dir, "cut_settings.json");

            if (!File.Exists(newPath))
            {
                string legacy = Path.Combine(legacyDir, "cut_settings.json");
                if (File.Exists(legacy))
                {
                    try { File.Copy(legacy, newPath); } catch { }
                }
            }
            return newPath;
        }

        /// <summary>
        /// 初回セットアップウィザードを表示すべきか。
        /// 未完了かつ Velopack インストール済みのときのみ表示する
        /// （ポータブル版・開発実行ではショートカット作成が無意味なため出さない）。
        /// </summary>
        public static bool ShouldShowSetupWizard(bool setupCompleted, bool isInstalled)
            => !setupCompleted && isInstalled;

        public AppSettings Current { get; private set; }

        public SettingsManager()
        {
            Current = new AppSettings
            {
                SaveDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "SCtool")
            };
        }

        public void Load()
        {
            if (File.Exists(_settingsFile))
            {
                try
                {
                    string json = File.ReadAllText(_settingsFile);
                    var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                    if (loaded != null) Current = loaded;
                }
                catch { }
            }

            Migrate(Current);
        }

        /// <summary>
        /// 旧形式（タイトルをキーにしたカット設定）を Targets へ移行する。
        /// 旧データには実行ファイルパスが無いため、タイトルのみを持つターゲットとして登録し、
        /// 実際にそのウィンドウを捕捉できた時点で exeパスを補完する（TargetRegistry.GetOrAdd）。
        /// 移行は冪等で、既に Targets に存在するものは重複登録しない。
        /// </summary>
        public static void Migrate(AppSettings settings)
        {
            if (settings == null) return;

            settings.Targets ??= new List<TargetInfo>();

            if (settings.CutSettings != null)
            {
                foreach (var (title, topCut) in settings.CutSettings)
                {
                    if (string.IsNullOrEmpty(title)) continue;

                    var existing = TargetRegistry.Find(settings.Targets, executablePath: null, title: title);
                    if (existing != null) continue;

                    settings.Targets.Add(new TargetInfo
                    {
                        ExecutablePath = "",
                        LastKnownTitle = title,
                        DisplayName = title,
                        TopCut = topCut,
                    });
                }

                // 移行元は消さずに残す。旧バージョンで開き直した場合に設定が失われないようにするため。
            }

            if (string.IsNullOrEmpty(settings.LastSelectedTargetKey) && !string.IsNullOrEmpty(settings.LastSelectedWindow))
            {
                var last = TargetRegistry.Find(settings.Targets, executablePath: null, title: settings.LastSelectedWindow);

                // 旧データにカット設定が無かったウィンドウも、選択状態は復元できるようにする
                last ??= TargetRegistry.GetOrAdd(settings.Targets, "", settings.LastSelectedWindow);

                settings.LastSelectedTargetKey = last.Key;
            }
        }

        public bool Save()
        {
            try
            {
                File.WriteAllText(_settingsFile, JsonSerializer.Serialize(Current));
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}