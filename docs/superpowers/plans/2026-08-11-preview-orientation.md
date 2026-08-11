# プレビュー枠の縦横対応 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** プレビュー枠を横モード（現状＝下部・全幅）と縦モード（片側に縦長配置）で切り替えられるようにし、縦向きアプリのスクショを大きく見せる。手動切替＋縦時の左右選択＋縦横比の自動切替（Off/Prompt/Force）を実装する。

**Architecture:** プレビュー要素（`ImgPreview`ほか）はコードビハインド4ファイルで x:Name 参照されるため、**要素を複製せず単一のツリーを保持**する（spec 案2）。非プレビューのカード群を1つのコンテナ、プレビューカードを別コンテナにまとめ、ルート Grid を「縦1列（横モード）」と「2カラム（縦モード）」で切り替えて両コンテナを再配置する。向き判定は純粋関数に分離してテストする。

**Tech Stack:** .NET 10 (net10.0-windows), WPF, xUnit（`Tests/`）。

## Global Constraints

- コメント・UI文言・ログは日本語（このリポジトリ規約。ハングル混入禁止）。
- 新規/変更UIは直書き色を使わず `DynamicResource`（Fluentテーマ）でテーマ連動。
- 対象フレームワーク `net10.0-windows`、SDK 10.0.x。
- テストは xUnit `[Fact]`/`[Theory]`、日本語メソッド名（既存 `Tests/*.cs` に倣う）。テスト対象の純粋ロジックは `Tests/SCtoolGui.Tests.csproj` に `<Compile Include="..\X.cs" Link="Source\X.cs"/>` でリンクして取り込む（プロジェクト参照ではない）。
- ビルド: `dotnet build SCtoolGui.csproj -c Debug`。テスト: `dotnet test Tests/SCtoolGui.Tests.csproj`。
- コミットは各タスク末尾。co-author 行を付す。
- ブランチは `feature/preview-orientation`（作成済み・spec コミット済み）。

## ファイル構成

**新規:**
- `PreviewOrientationLogic.cs` — 向き判定・自動切替の決定を行う純粋ロジック（テスト対象）。
- `Tests/PreviewOrientationLogicTests.cs`
- `Tests/PreviewSettingsTests.cs` — 追加設定の既定/シリアライズ往復。

**変更:**
- `SettingsManager.cs` — `AppSettings` に縦横関連プロパティ追加。
- `MainWindow.xaml` — 非プレビューカードを `OperationsPanel` に、プレビューを `PreviewCard` にまとめ、ルートに横用/縦用の2レイアウト土台を用意。プレビュー見出しに [縦⇄横] ボタン追加。
- `MainWindow.xaml.cs` — レイアウト適用 `ApplyPreviewOrientation()`、モード別窓サイズの保存/復元、[縦⇄横] ハンドラ。
- `MainWindow.Windows.cs` — `CmbWindows_SelectionChanged` に自動切替（判定はウィンドウ切替時のみ）。
- `MainWindow.Preview.cs` — `ShowPreview` で最後に表示した画像の縦横を記録（自動切替の判定材料）。
- `SettingsWindow.xaml` / `SettingsWindow.xaml.cs` — 縦時の左右・自動切替モードの設定UI追加。
- `MainWindow.xaml.cs`（`BtnSettings_Click` 付近）— SettingsWindow との受け渡しに新項目を追加。

---

## Task 1: 設定モデルに縦横プロパティを追加

**Files:**
- Modify: `SettingsManager.cs`（`AppSettings`、`SetupCompleted` の後に追加）
- Modify: `Tests/SCtoolGui.Tests.csproj`（後続タスクで使うためこの時点では不要。ここでは AppSettings のみ）
- Test: `Tests/PreviewSettingsTests.cs`（新規）

**Interfaces:**
- Produces（`AppSettings` の新プロパティ、既定値つき）:
  - `string PreviewOrientation = "Horizontal"`（"Horizontal"/"Vertical"）
  - `string VerticalPreviewSide = "Right"`（"Right"/"Left"）
  - `string PreviewAutoSwitch = "Prompt"`（"Off"/"Prompt"/"Force"）
  - `double? HorizontalWindowWidth = null`、`double? HorizontalWindowHeight = null`
  - `double? VerticalWindowWidth = null`、`double? VerticalWindowHeight = null`

- [ ] **Step 1: 失敗するテストを書く**

`Tests/PreviewSettingsTests.cs`:
```csharp
using System.Text.Json;
using SCtoolGui;

namespace SCtoolGui.Tests
{
    public class PreviewSettingsTests
    {
        [Fact]
        public void プレビュー向きの既定はHorizontal()
        {
            Assert.Equal("Horizontal", new AppSettings().PreviewOrientation);
        }

        [Fact]
        public void 縦時の左右の既定はRight()
        {
            Assert.Equal("Right", new AppSettings().VerticalPreviewSide);
        }

        [Fact]
        public void 自動切替の既定はPrompt()
        {
            Assert.Equal("Prompt", new AppSettings().PreviewAutoSwitch);
        }

        [Fact]
        public void 縦横プロパティはシリアライズ往復で保持される()
        {
            var s = new AppSettings
            {
                PreviewOrientation = "Vertical",
                VerticalPreviewSide = "Left",
                PreviewAutoSwitch = "Force",
                VerticalWindowWidth = 500,
                VerticalWindowHeight = 900,
            };
            var back = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(s))!;
            Assert.Equal("Vertical", back.PreviewOrientation);
            Assert.Equal("Left", back.VerticalPreviewSide);
            Assert.Equal("Force", back.PreviewAutoSwitch);
            Assert.Equal(500, back.VerticalWindowWidth);
            Assert.Equal(900, back.VerticalWindowHeight);
        }

        [Fact]
        public void 旧設定JSONに項目が無ければ既定になる()
        {
            var back = JsonSerializer.Deserialize<AppSettings>("{\"SaveDirectory\":\"C:/x\"}")!;
            Assert.Equal("Horizontal", back.PreviewOrientation);
            Assert.Equal("Right", back.VerticalPreviewSide);
            Assert.Equal("Prompt", back.PreviewAutoSwitch);
        }
    }
}
```

- [ ] **Step 2: 失敗を確認**

Run: `dotnet test Tests/SCtoolGui.Tests.csproj --filter PreviewSettingsTests`
Expected: コンパイルエラー（プロパティ未定義）

- [ ] **Step 3: 実装**

`SettingsManager.cs` の `AppSettings`、`public bool SetupCompleted ...` の直後に追加:
```csharp
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
```

- [ ] **Step 4: テスト成功を確認**

Run: `dotnet test Tests/SCtoolGui.Tests.csproj --filter PreviewSettingsTests`
Expected: PASS（5メソッド）

- [ ] **Step 5: コミット**

```bash
git add SettingsManager.cs Tests/PreviewSettingsTests.cs
git commit -m "プレビュー縦横の設定プロパティを追加

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 2: 向き判定・自動切替の純粋ロジック

**Files:**
- Create: `PreviewOrientationLogic.cs`
- Modify: `Tests/SCtoolGui.Tests.csproj`（`<Compile Include="..\PreviewOrientationLogic.cs" Link="Source\PreviewOrientationLogic.cs" />` を追加）
- Test: `Tests/PreviewOrientationLogicTests.cs`

**Interfaces:**
- Produces:
  - `enum PreviewMode { Horizontal, Vertical }`
  - `enum AutoSwitchDecision { None, Prompt, Switch }`
  - `static class PreviewOrientationLogic`
    - `static PreviewMode DetectImageOrientation(double width, double height)` — 高さ>幅なら Vertical、それ以外（正方形含む）Horizontal。
    - `static AutoSwitchDecision Decide(string autoSwitchSetting, PreviewMode current, PreviewMode image)`
      - 設定 "Off" → None
      - current==image → None（一致なら何もしない）
      - "Force" かつ不一致 → Switch
      - "Prompt" かつ不一致 → Prompt
      - その他 → None

抑制（対象ごと1回まで）は呼び出し側で対象キー集合を持って管理する（このロジックは純粋な決定のみ）。

- [ ] **Step 1: 失敗するテストを書く**

`Tests/PreviewOrientationLogicTests.cs`:
```csharp
using SCtoolGui;

namespace SCtoolGui.Tests
{
    public class PreviewOrientationLogicTests
    {
        [Theory]
        [InlineData(1920, 1080, PreviewMode.Horizontal)]
        [InlineData(1080, 1920, PreviewMode.Vertical)]
        [InlineData(500, 500, PreviewMode.Horizontal)] // 正方形は横扱い
        public void 画像の向き判定(double w, double h, PreviewMode expected)
        {
            Assert.Equal(expected, PreviewOrientationLogic.DetectImageOrientation(w, h));
        }

        [Fact]
        public void Off設定は常にNone()
        {
            Assert.Equal(AutoSwitchDecision.None,
                PreviewOrientationLogic.Decide("Off", PreviewMode.Horizontal, PreviewMode.Vertical));
        }

        [Fact]
        public void 一致していればNone()
        {
            Assert.Equal(AutoSwitchDecision.None,
                PreviewOrientationLogic.Decide("Force", PreviewMode.Vertical, PreviewMode.Vertical));
        }

        [Fact]
        public void Force不一致はSwitch()
        {
            Assert.Equal(AutoSwitchDecision.Switch,
                PreviewOrientationLogic.Decide("Force", PreviewMode.Horizontal, PreviewMode.Vertical));
        }

        [Fact]
        public void Prompt不一致はPrompt()
        {
            Assert.Equal(AutoSwitchDecision.Prompt,
                PreviewOrientationLogic.Decide("Prompt", PreviewMode.Horizontal, PreviewMode.Vertical));
        }
    }
}
```

- [ ] **Step 2: 失敗を確認**

Run: `dotnet test Tests/SCtoolGui.Tests.csproj --filter PreviewOrientationLogicTests`
Expected: コンパイルエラー（型未定義）

- [ ] **Step 3: 実装（＋Testsにリンク追加）**

`Tests/SCtoolGui.Tests.csproj` の `<Compile Include>` 群に追加:
```xml
    <Compile Include="..\PreviewOrientationLogic.cs" Link="Source\PreviewOrientationLogic.cs" />
```

`PreviewOrientationLogic.cs`:
```csharp
namespace SCtoolGui
{
    public enum PreviewMode { Horizontal, Vertical }

    public enum AutoSwitchDecision { None, Prompt, Switch }

    /// <summary>プレビューの向き判定と自動切替の決定を行う純粋ロジック。</summary>
    public static class PreviewOrientationLogic
    {
        public static PreviewMode DetectImageOrientation(double width, double height)
            => height > width ? PreviewMode.Vertical : PreviewMode.Horizontal;

        public static AutoSwitchDecision Decide(string autoSwitchSetting, PreviewMode current, PreviewMode image)
        {
            if (autoSwitchSetting == "Off") return AutoSwitchDecision.None;
            if (current == image) return AutoSwitchDecision.None;
            if (autoSwitchSetting == "Force") return AutoSwitchDecision.Switch;
            if (autoSwitchSetting == "Prompt") return AutoSwitchDecision.Prompt;
            return AutoSwitchDecision.None;
        }
    }
}
```

- [ ] **Step 4: テスト成功を確認**

Run: `dotnet test Tests/SCtoolGui.Tests.csproj --filter PreviewOrientationLogicTests`
Expected: PASS（3 Theory ケース＋4 Fact）

- [ ] **Step 5: コミット**

```bash
git add PreviewOrientationLogic.cs Tests/PreviewOrientationLogicTests.cs Tests/SCtoolGui.Tests.csproj
git commit -m "プレビュー向き判定・自動切替の純粋ロジックを追加

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 3: XAML を横/縦レイアウト切替可能な構造へ再編

**Files:**
- Modify: `MainWindow.xaml`（ルート Grid `:66-` と各カード、プレビュー見出し `:197`）

**Interfaces:**
- Produces（x:Name。後続タスクが参照）:
  - `Grid RootLayoutGrid`（ルート。行/列を切替）
  - `StackPanel OperationsPanel`（更新バナー＋ウィンドウ指定＋操作行＋ログをまとめる）
  - `Border PreviewCard`（プレビューカード。既存の中身をそのまま内包）
  - `Button BtnTogglePreviewOrientation`（[縦⇄横] ボタン。見出し行）

**方針:** 要素を複製しない。現在ルート直下に並ぶ「更新バナー/ウィンドウ指定/操作行/プレビュー/ログ」のうち、**プレビュー以外を `OperationsPanel`（StackPanel）に入れ**、プレビューを `PreviewCard` にする。ルートは既定（横）では OperationsPanel（上）→ PreviewCard（下）を行で並べる。縦モードでは Task 5 のコードで列配置へ組み替える。ここでは**横モードで従来と同じ見た目になる**ことをゴールにする（切替ロジックは Task 5）。

- [ ] **Step 1: ルート Grid とコンテナを再編**

`MainWindow.xaml` のルート `<Grid Margin="16">`（`:66`）を次の構造へ変更する。行定義は「OperationsPanel(Auto) / PreviewCard(*)」の2行にする（従来の5行個別配置をやめ、非プレビューを1つの StackPanel へ集約）:

```xml
    <Grid x:Name="RootLayoutGrid" Margin="16">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>  <!-- OperationsPanel -->
            <RowDefinition Height="*"/>     <!-- PreviewCard -->
        </Grid.RowDefinitions>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="*"/>
        </Grid.ColumnDefinitions>

        <StackPanel x:Name="OperationsPanel" Grid.Row="0" Grid.Column="0">
            <!-- ここに従来の 更新バナー / ウィンドウ指定 / 操作行 を移動（Grid.Row 属性は削除） -->
        </StackPanel>

        <!-- PreviewCard: 従来のプレビュー Border（Grid.Row=3 を Grid.Row=1 に） -->

        <!-- ログは OperationsPanel の最後に移動（下記 Step 2 参照） -->
    </Grid>
```

具体手順:
1. 既存の更新バナー Border（`:76`）から `Grid.Row="0"` を削除し、`OperationsPanel` の子に移動。
2. ウィンドウ指定 Border（`:94`）から `Grid.Row="1"` を削除し `OperationsPanel` の子へ。
3. 操作行 Grid（`:150`）から `Grid.Row="2"` を削除し `OperationsPanel` の子へ。
4. プレビュー Border（`:189`）を `x:Name="PreviewCard"` に、`Grid.Row="3"` を `Grid.Row="1"` に変更。ルート直下に残す。
5. ログ Border（`:272`）から `Grid.Row="4"` を削除し `OperationsPanel` の**最後の子**へ移動（横モードでプレビューの下に来ないが、縦モードでは操作群側に付く。横モードでの見た目は「操作群→プレビュー」の順で従来と近い。**ただし横モードでログをプレビュー下に出したい場合**は Task 5 でモードに応じてログの配置先を切替える。まずはこの Step では OperationsPanel 末尾に置く）。

注: 横モードでのログ位置が従来（プレビューの下）と変わる点は、Task 5 でモード別にログを移動させることで解消する。この Step のゴールはビルドが通り横モードで各カードが表示されること。

- [ ] **Step 2: プレビュー見出しに [縦⇄横] ボタンを追加**

プレビュー見出し行（`:197` の `<TextBlock ... Text="プレビュー"/>`）を、タイトルとボタンを横に並べる Grid へ変更:
```xml
                <Grid Grid.Row="0">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="Auto"/>
                    </Grid.ColumnDefinitions>
                    <TextBlock Grid.Column="0" Style="{StaticResource CardTitle}" Text="プレビュー" VerticalAlignment="Center"/>
                    <Button x:Name="BtnTogglePreviewOrientation" Grid.Column="1" Padding="8,3"
                            Click="BtnTogglePreviewOrientation_Click"
                            ToolTip="プレビューの縦／横を切り替えます">
                        <StackPanel Orientation="Horizontal">
                            <TextBlock Style="{StaticResource Icon}" Text="&#xE745;" Margin="0,0,6,0"/>
                            <TextBlock Text="縦⇄横" VerticalAlignment="Center"/>
                        </StackPanel>
                    </Button>
                </Grid>
```
（`&#xE745;` は回転系アイコン。存在しない場合は任意の Segoe グリフに置換。）

- [ ] **Step 3: ハンドラの仮実装（ビルドを通すため）**

`MainWindow.xaml.cs` に空ハンドラを追加（実装は Task 5）:
```csharp
        private void BtnTogglePreviewOrientation_Click(object sender, RoutedEventArgs e)
        {
            // Task 5 で実装
        }
```

- [ ] **Step 4: ビルド確認**

Run: `dotnet build SCtoolGui.csproj -c Debug`
Expected: 成功。

- [ ] **Step 5: 実機スモーク（横モードで従来通り表示）**

Run: `dotnet run --project SCtoolGui.csproj`
確認: 起動し、ウィンドウ指定・カット・キャプチャ・プレビュー・ログが表示される。プレビュー見出しに [縦⇄横] ボタンが出る。閉じる。

- [ ] **Step 6: コミット**

```bash
git add MainWindow.xaml MainWindow.xaml.cs
git commit -m "プレビューを横/縦で切替可能なレイアウト構造へ再編(横モードは従来通り)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 4: 最後に表示した画像の向きを記録

**Files:**
- Modify: `MainWindow.Preview.cs`（`ShowPreview` `:134`）

**Interfaces:**
- Produces: `private PreviewMode? _lastShownImageOrientation`（`MainWindow` フィールド。最後に `ShowPreview` した画像の向き）

自動切替（Task 6）は「ウィンドウ切替時に対象のプレビュー画像の向き」を必要とする。`ShowPreview` は `ImgPreview.Source = LoadBitmap(filePath)` で画像を読むので、その寸法から向きを記録する。

- [ ] **Step 1: フィールドと記録を追加**

`MainWindow.Preview.cs` のクラス先頭付近（`_isTempPreviewMode` 等の近く）に追加:
```csharp
        /// <summary>最後に ShowPreview した画像の向き（自動切替の判定材料）。</summary>
        private PreviewMode? _lastShownImageOrientation;
```

`ShowPreview` 内、`ImgPreview.Source = LoadBitmap(filePath);` の直後に追加:
```csharp
            if (ImgPreview.Source is System.Windows.Media.Imaging.BitmapSource bmp)
            {
                _lastShownImageOrientation =
                    PreviewOrientationLogic.DetectImageOrientation(bmp.PixelWidth, bmp.PixelHeight);
            }
```

- [ ] **Step 2: ビルド確認**

Run: `dotnet build SCtoolGui.csproj -c Debug`
Expected: 成功。

- [ ] **Step 3: コミット**

```bash
git add MainWindow.Preview.cs
git commit -m "最後に表示したプレビュー画像の向きを記録

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 5: レイアウト適用とモード別窓サイズ、手動切替

**Files:**
- Modify: `MainWindow.xaml.cs`（`ApplyPreviewOrientation`、窓サイズ保存/復元、`BtnTogglePreviewOrientation_Click`、コンストラクタ、`OnClosed`）

**Interfaces:**
- Consumes: `RootLayoutGrid` / `OperationsPanel` / `PreviewCard`（Task 3）, `AppSettings.PreviewOrientation` 他（Task 1）
- Produces: `private void ApplyPreviewOrientation(PreviewMode mode)` — レイアウトを組み替え、窓サイズを該当モードへ。

**方針:** `RootLayoutGrid` の行/列定義と、`OperationsPanel`・`PreviewCard` の `Grid.Row/Column` を切替える。
- 横モード: 1列2行。OperationsPanel=(Row0,Col0)、PreviewCard=(Row1,Col0)。
- 縦モード: 2列1行。`VerticalPreviewSide=="Right"` なら OperationsPanel=(Col0)・PreviewCard=(Col1)、"Left" なら逆。行は1行（`*`）。

- [ ] **Step 1: ApplyPreviewOrientation を実装**

`MainWindow.xaml.cs` に追加:
```csharp
        private PreviewMode CurrentPreviewMode =>
            _settingsManager.Current.PreviewOrientation == "Vertical"
                ? PreviewMode.Vertical : PreviewMode.Horizontal;

        /// <summary>プレビューの向きに応じてルートレイアウトを組み替え、窓サイズを合わせる。</summary>
        private void ApplyPreviewOrientation(PreviewMode mode)
        {
            RootLayoutGrid.RowDefinitions.Clear();
            RootLayoutGrid.ColumnDefinitions.Clear();

            if (mode == PreviewMode.Horizontal)
            {
                RootLayoutGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                RootLayoutGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                RootLayoutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                Grid.SetRow(OperationsPanel, 0); Grid.SetColumn(OperationsPanel, 0);
                Grid.SetRow(PreviewCard, 1); Grid.SetColumn(PreviewCard, 0);
                PreviewCard.Margin = new Thickness(0);
            }
            else
            {
                RootLayoutGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                bool previewRight = _settingsManager.Current.VerticalPreviewSide != "Left";
                // 操作群は幅を抑え、プレビューを広めに
                var opCol = new ColumnDefinition { Width = new GridLength(360) };
                var prevCol = new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) };

                if (previewRight)
                {
                    RootLayoutGrid.ColumnDefinitions.Add(opCol);   // Col0=操作
                    RootLayoutGrid.ColumnDefinitions.Add(prevCol); // Col1=プレビュー
                    Grid.SetColumn(OperationsPanel, 0); Grid.SetColumn(PreviewCard, 1);
                    PreviewCard.Margin = new Thickness(12, 0, 0, 0);
                }
                else
                {
                    RootLayoutGrid.ColumnDefinitions.Add(prevCol); // Col0=プレビュー
                    RootLayoutGrid.ColumnDefinitions.Add(opCol);   // Col1=操作
                    Grid.SetColumn(PreviewCard, 0); Grid.SetColumn(OperationsPanel, 1);
                    PreviewCard.Margin = new Thickness(0, 0, 12, 0);
                }
                Grid.SetRow(OperationsPanel, 0); Grid.SetRow(PreviewCard, 0);
            }

            ApplyWindowSizeForMode(mode);
        }

        /// <summary>モードに対応する保存済み窓サイズを適用する（無ければ既定サイズ）。</summary>
        private void ApplyWindowSizeForMode(PreviewMode mode)
        {
            var s = _settingsManager.Current;
            if (mode == PreviewMode.Horizontal)
            {
                if (s.HorizontalWindowWidth.HasValue) this.Width = s.HorizontalWindowWidth.Value;
                if (s.HorizontalWindowHeight.HasValue) this.Height = s.HorizontalWindowHeight.Value;
            }
            else
            {
                // 縦モード既定は縦長め（保存が無ければ 560x900 を初期提示）
                this.Width = s.VerticalWindowWidth ?? 560;
                this.Height = s.VerticalWindowHeight ?? 900;
            }
        }

        /// <summary>現在の窓サイズを現在モードのサイズとして記憶する。</summary>
        private void SaveWindowSizeForCurrentMode()
        {
            if (this.WindowState != WindowState.Normal) return;
            var s = _settingsManager.Current;
            if (CurrentPreviewMode == PreviewMode.Horizontal)
            {
                s.HorizontalWindowWidth = this.Width; s.HorizontalWindowHeight = this.Height;
            }
            else
            {
                s.VerticalWindowWidth = this.Width; s.VerticalWindowHeight = this.Height;
            }
        }
```

- [ ] **Step 2: 手動切替ハンドラを実装**

Task 3 で置いた空ハンドラを差し替え:
```csharp
        private void BtnTogglePreviewOrientation_Click(object sender, RoutedEventArgs e)
        {
            // 切替前に現在モードのサイズを保存
            SaveWindowSizeForCurrentMode();

            var next = CurrentPreviewMode == PreviewMode.Horizontal
                ? PreviewMode.Vertical : PreviewMode.Horizontal;
            _settingsManager.Current.PreviewOrientation =
                next == PreviewMode.Vertical ? "Vertical" : "Horizontal";
            ApplyPreviewOrientation(next);
            _settingsManager.Save();
        }
```

- [ ] **Step 3: 起動時に適用**

コンストラクタ（`MainWindow.xaml.cs:34` `InitializeWindowList();` の前）に追加:
```csharp
            ApplyPreviewOrientation(CurrentPreviewMode);
```

- [ ] **Step 4: 終了時にサイズ保存**

`OnClosed`（`:43-47` の位置保存の直後）に追加:
```csharp
            SaveWindowSizeForCurrentMode();
```

- [ ] **Step 5: ビルド確認**

Run: `dotnet build SCtoolGui.csproj -c Debug`
Expected: 成功。

- [ ] **Step 6: 実機確認（手動切替）**

Run: `dotnet run --project SCtoolGui.csproj`
確認:
1. [縦⇄横] ボタンで縦モード→操作群が片側・プレビューが反対側の縦長、窓が縦長にリサイズ。
2. もう一度押すと横モードへ戻り窓サイズも戻る。
3. 縦モードのまま閉じ、再起動→縦モードで復元される。

- [ ] **Step 7: コミット**

```bash
git add MainWindow.xaml.cs
git commit -m "プレビュー横/縦のレイアウト切替とモード別窓サイズ記憶を実装

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 6: ウィンドウ切替時の自動切替（Off/Prompt/Force）

**Files:**
- Modify: `MainWindow.Windows.cs`（`CmbWindows_SelectionChanged` `:127`、切替確定後 `SaveAndLog("ウィンドウを...")` `:151` 付近）
- Modify: `MainWindow.xaml.cs`（抑制用の対象キー集合フィールド）

**Interfaces:**
- Consumes: `PreviewOrientationLogic.Decide`（Task 2）, `_lastShownImageOrientation`（Task 4）, `ApplyPreviewOrientation`（Task 5）, `AppSettings.PreviewAutoSwitch`
- Produces: `private readonly HashSet<string> _autoSwitchPromptedTargets`（Prompt を出した対象キーの記録。対象ごと1回まで）

**方針:** ウィンドウ切替が確定し、その対象のプレビューが撮れた後（`CaptureTempPreview` 実行後）に、`_lastShownImageOrientation` と現在モードから `Decide` する。判定はウィンドウ切替時のみ（キャプチャ・更新では呼ばない）。

- [ ] **Step 1: 抑制用フィールドを追加**

`MainWindow.xaml.cs` に追加:
```csharp
        /// <summary>Prompt を既に出した対象キー（対象ごと1回まで誘導するため）。</summary>
        private readonly System.Collections.Generic.HashSet<string> _autoSwitchPromptedTargets = new();
```

- [ ] **Step 2: 自動切替の適用メソッドを追加**

`MainWindow.Windows.cs` に追加（`CmbWindows_SelectionChanged` と同じクラス）:
```csharp
        /// <summary>ウィンドウ切替時に、対象画像の向きに応じて自動切替（Off/Prompt/Force）を行う。</summary>
        private void MaybeAutoSwitchOrientation(TargetInfo target)
        {
            if (_lastShownImageOrientation is not PreviewMode imageMode) return;

            var decision = PreviewOrientationLogic.Decide(
                _settingsManager.Current.PreviewAutoSwitch, CurrentPreviewMode, imageMode);

            if (decision == AutoSwitchDecision.None) return;

            if (decision == AutoSwitchDecision.Switch)
            {
                SwitchOrientationTo(imageMode);
                return;
            }

            // Prompt: 対象ごとに1回だけ
            if (_autoSwitchPromptedTargets.Contains(target.Key)) return;
            _autoSwitchPromptedTargets.Add(target.Key);

            string dir = imageMode == PreviewMode.Vertical ? "縦向き" : "横向き";
            var ask = MessageBox.Show(
                $"この画像は{dir}です。プレビューを{dir}に切り替えますか？",
                "プレビューの向き", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (ask == MessageBoxResult.Yes) SwitchOrientationTo(imageMode);
        }

        /// <summary>指定モードへ切替（手動切替と同じくサイズ保存→適用→設定保存）。</summary>
        private void SwitchOrientationTo(PreviewMode mode)
        {
            if (CurrentPreviewMode == mode) return;
            SaveWindowSizeForCurrentMode();
            _settingsManager.Current.PreviewOrientation =
                mode == PreviewMode.Vertical ? "Vertical" : "Horizontal";
            ApplyPreviewOrientation(mode);
            _settingsManager.Save();
        }
```

- [ ] **Step 3: ウィンドウ切替確定後に呼ぶ**

`CmbWindows_SelectionChanged` の `CaptureTempPreview(verbose: false);`（`:170`）の直後に追加:
```csharp
                MaybeAutoSwitchOrientation(target);
```
（`target` はこのスコープで `var target = TargetRegistry.GetOrAdd(...)` により得られている変数。`CmbWindows_SelectionChanged` 内の `CaptureTempPreview(verbose: false);` と同一スコープにあることを確認済み。）

- [ ] **Step 4: ビルド確認**

Run: `dotnet build SCtoolGui.csproj -c Debug`
Expected: 成功。

- [ ] **Step 5: 実機確認（自動切替）**

Run: `dotnet run --project SCtoolGui.csproj`
確認（詳細設定UIは Task 7。ここでは既定 Prompt で）:
1. 横モードのまま、縦向きウィンドウを選択 → 「縦向きに切り替えますか？」が出る。「はい」で縦モードへ。
2. 同じ対象を選び直しても再度は出ない（対象ごと1回）。
3. （設定JSONを手で "Force" にして）縦向き選択で確認なしに縦へ。"Off" で何も起きない。

- [ ] **Step 6: コミット**

```bash
git add MainWindow.Windows.cs MainWindow.xaml.cs
git commit -m "ウィンドウ切替時の縦横自動切替(Off/Prompt/Force)を実装

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 7: 詳細設定に「縦時の左右」「自動切替モード」を追加

**Files:**
- Modify: `SettingsWindow.xaml`（項目追加）
- Modify: `SettingsWindow.xaml.cs`（コンストラクタ引数・`Result*` プロパティ・`BtnSave_Click`）
- Modify: `MainWindow.xaml.cs`（`BtnSettings_Click` の受け渡し）

**Interfaces:**
- Produces（`SettingsWindow`）:
  - コンストラクタに `string verticalPreviewSide, string previewAutoSwitch` を追加
  - `string ResultVerticalPreviewSide { get; private set; }`
  - `string ResultPreviewAutoSwitch { get; private set; }`

- [ ] **Step 1: SettingsWindow.xaml に項目追加**

テーマ選択の StackPanel（`SettingsWindow.xaml:41-48`）の後に追加:
```xml
            <StackPanel Orientation="Horizontal" Margin="0,0,0,10" VerticalAlignment="Center">
                <TextBlock Text="縦モード時のプレビュー位置:" VerticalAlignment="Center" Margin="0,0,10,0"/>
                <ComboBox x:Name="CmbVerticalSide" Width="120">
                    <ComboBoxItem Content="右"/>
                    <ComboBoxItem Content="左"/>
                </ComboBox>
            </StackPanel>

            <StackPanel Orientation="Horizontal" Margin="0,0,0,15" VerticalAlignment="Center">
                <TextBlock Text="縦横の自動切替:" VerticalAlignment="Center" Margin="0,0,10,0"/>
                <ComboBox x:Name="CmbAutoSwitch" Width="200">
                    <ComboBoxItem Content="切り替えない"/>
                    <ComboBoxItem Content="確認して切り替え"/>
                    <ComboBoxItem Content="自動で切り替え"/>
                </ComboBox>
            </StackPanel>
```

- [ ] **Step 2: SettingsWindow.xaml.cs にプロパティと読み書きを追加**

コンストラクタ引数へ `string verticalPreviewSide, string previewAutoSwitch` を追加し、読込部（`ChkAlwaysRunAsAdmin.IsChecked = ...` 付近）に:
```csharp
            CmbVerticalSide.SelectedIndex = verticalPreviewSide == "Left" ? 1 : 0;
            CmbAutoSwitch.SelectedIndex = previewAutoSwitch switch
            {
                "Off" => 0,
                "Force" => 2,
                _ => 1, // Prompt
            };
```
`Result*` プロパティを追加:
```csharp
        public string ResultVerticalPreviewSide { get; private set; } = "Right";
        public string ResultPreviewAutoSwitch { get; private set; } = "Prompt";
```
`BtnSave_Click` に:
```csharp
            ResultVerticalPreviewSide = CmbVerticalSide.SelectedIndex == 1 ? "Left" : "Right";
            ResultPreviewAutoSwitch = CmbAutoSwitch.SelectedIndex switch
            {
                0 => "Off",
                2 => "Force",
                _ => "Prompt",
            };
```

- [ ] **Step 3: MainWindow 側の受け渡し**

`MainWindow.xaml.cs` の `BtnSettings_Click` で SettingsWindow を生成している箇所に、新引数を渡し、保存結果を反映する。既存の引数リストへ:
```csharp
                _settingsManager.Current.VerticalPreviewSide,
                _settingsManager.Current.PreviewAutoSwitch
```
を追加する（既存の生成は `var settingsWin = new SettingsWindow(...)` で末尾引数が `IconPath`。その後ろにカンマ区切りで2引数追加）。`if (settingsWin.ShowDialog() == true)` ブロック内、他の `Result*` を反映している箇所に:
```csharp
                _settingsManager.Current.VerticalPreviewSide = settingsWin.ResultVerticalPreviewSide;
                _settingsManager.Current.PreviewAutoSwitch = settingsWin.ResultPreviewAutoSwitch;
                // 左右が変わった場合、縦モードなら再適用して反映
                if (CurrentPreviewMode == PreviewMode.Vertical) ApplyPreviewOrientation(PreviewMode.Vertical);
```

- [ ] **Step 4: ビルド確認**

Run: `dotnet build SCtoolGui.csproj -c Debug`
Expected: 成功。

- [ ] **Step 5: 実機確認（詳細設定）**

Run: `dotnet run --project SCtoolGui.csproj`
確認: 詳細設定で「縦モード時の位置」を左に、「自動切替」を各値に変更・保存→縦モードでプレビューが左に来る／自動切替の挙動が設定通りになる。設定が再起動後も保持される。

- [ ] **Step 6: コミット**

```bash
git add SettingsWindow.xaml SettingsWindow.xaml.cs MainWindow.xaml.cs
git commit -m "詳細設定に縦モードの左右と縦横自動切替モードを追加

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 8: ログ配置のモード別調整と全体検証

**Files:**
- Modify: `MainWindow.xaml.cs`（`ApplyPreviewOrientation` にログ配置の調整が必要なら追加）

Task 3 でログを `OperationsPanel` 末尾に置いた。横モードでは従来「プレビューの下」だったが、集約により操作群側に付く。**横モードでログを従来位置（プレビュー下＝ウィンドウ最下部）に戻したい**場合の対応をここで判断する。

- [ ] **Step 1: 横モードのログ位置を確認**

Run: `dotnet run --project SCtoolGui.csproj`
横モードでログの位置が許容範囲か目視。許容ならこのタスクは調整不要（Step 3 へ）。

- [ ] **Step 2: （必要な場合のみ）モード別にログを移動**

もし横モードでログを最下部へ戻すなら、`OperationsPanel` からログ Border を分離して名前を付け（`LogCard`）、`ApplyPreviewOrientation` で:
- 横モード: `LogCard` をルート最下行へ（行を1つ増やし PreviewCard の下）。
- 縦モード: `LogCard` を `OperationsPanel` 末尾へ。
実装するなら `OperationsPanel`/`LogCard` の親子付け替えを WPF の `Children.Remove`/`Add` で行う。ビルド確認 `dotnet build SCtoolGui.csproj -c Debug`。

- [ ] **Step 3: 全テスト**

Run: `dotnet test Tests/SCtoolGui.Tests.csproj`
Expected: すべて PASS（既存＋ PreviewSettings＋ PreviewOrientationLogic）。

- [ ] **Step 4: リリース相当ビルド**

Run: `dotnet publish SCtoolGui.csproj -c Release -r win-x64 --self-contained true -p:Version=1.0.6 -o "$TEMP/sctool-verify-106"`
Expected: 成功し `SCtoolGui.exe` 生成。

- [ ] **Step 5: 実機総合確認**

Run: `dotnet run --project SCtoolGui.csproj`
確認: 横⇄縦手動切替、縦時の左右、モード別窓サイズ復元、自動切替（Prompt/Force/Off）、詳細設定反映が一通り動く。

- [ ] **Step 6: コミット（変更があれば）**

```bash
git add -A
git commit -m "ログ配置のモード別調整と総合検証

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## 実装後の完了確認

- [ ] 手動切替（A）・縦時の左右（B）・自動切替 Off/Prompt/Force（C）が spec 通り動く
- [ ] モード別に窓サイズが記憶・復元される
- [ ] `dotnet test` 全 PASS
- [ ] 別ウィンドウ分離は未着手（別 spec）
- [ ] リリースは未実施（タグ push はユーザー判断。`release-flow-tag-push-ci` 参照）
