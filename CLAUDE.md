# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```powershell
dotnet build                                  # ソリューション全体
dotnet run --project StS2Toys                 # セーブデータビューア
dotnet run --project StS2CardBrowser          # カードブラウザ
dotnet run --project card-type-extractor      # カードメタデータ再生成（要ゲームDLL）
```

テスト・lint コマンドは設定されていない。

## Tools Setup（一回限り・git 管理外）

`tools/` ディレクトリは未追跡。ゲームの `.pck` ファイルからアセットを展開する必要がある：

```powershell
# PCKExplorer でゲームアセットを展開（約 1.8 GB）
$pck = "C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\SlayTheSpire2.pck"
.\tools\GodotPCKExplorer\GodotPCKExplorer.Console.exe -e $pck .\tools\extracted
```

展開後は `tools/extracted/` に以下が生成される：

- `localization/{eng,jpn,deu,...}/*.json` — ゲーム全テキスト
- `images/events/`, `images/card_portraits_png/` — 画像ファイル（`.png.import` + `.ctex`）

画像は BC7 圧縮テクスチャ（`.ctex`）で、StS2SiteBuilder のビルド時に PNG 変換してキャッシュする。

## Architecture

本リポジトリは .NET 10 / Windows Forms のデスクトップアプリ群で、Slay the Spire 2 のゲームデータを参照・閲覧する。

### プロジェクト構成

| プロジェクト | 役割 |
|---|---|
| `StS2Shared` | 全アプリが参照する共有ライブラリ（サービス・メカニクス定義） |
| `StS2Toys` | セーブデータビューア（デッキ・レリック表示） |
| `StS2CardBrowser` | カードブラウザ（キャラクター・メカニクスフィルタ付き） |
| `card-type-extractor` | ゲーム DLL の IL を解析してカードメタデータ JSON を生成するCLIツール |
| `SpineRuntime` | spine-csharp の pure C# ランタイム（データ構造のみ、描画なし） |

### StS2Shared — 共有ライブラリ

`StS2Shared` は全アプリが `ProjectReference` で参照する。埋め込みリソースとして以下を保持する：

**`Resources/*.json`（手動管理 or card-type-extractor 生成）**
- `card_database.json` — カード・レリックの EN/JP 表示名。ローカライズの `{ID}.title` から card-type-extractor がバージョンフォルダ（`Resources/{version}/`）へ生成
- `card_descriptions.json` — カードの EN/JP 説明文（生テキスト＝`[gold]`タグや`{Var}`テンプレート保持）。ローカライズの `{ID}.description` から生成。`GetDescription` とシナジー判定の読み元（cards.json 埋め込みから移行）
- `card_types.json`, `card_costs.json`, `card_rarities.json`, `card_characters.json` — ゲーム DLL から抽出
- `card_star_costs.json` — スターコストを持つカードの ID リスト（`get_CanonicalStarCost > 0` または `get_HasStarCostX` が true のもの）
- `card_upgraded_costs.json` — アップグレードでコストが変わるカードのみ収録（`OnUpgrade` の `EnergyCost.UpgradeBy/To` から抽出）。`CardDatabaseService.GetUpgradedCost(Value)` で参照
- `card_stats.json` — カードのキャノニカル変数（ダメージ・ブロック値など）
- `card_images.json` — カード ID → 画像のソース相対パス（`card_portraits_png/` 基準、例 `silent/abrasive.png`）。
  extractor が実ファイルをスキャンして生成。`Services/CardImageService.cs` で参照し、SiteBuilder/Toys 双方の画像解決を一元化
- `card_related.json` — カードがホバー表示する関連カード（DLL の `get_ExtraHoverTips`、カードのみにフィルタ）。例: `CARD.ACCURACY` → `[CARD.SHIV]`。`GetRelatedCards` / `GetCreatedByCards`（逆引き）で参照

**ローカライゼーション JSON（バージョン管理 = `Resources/{version}/localization/{eng,jpn}/`）**
- `relics` / `card_keywords` / `afflictions` / `enchantments` / `encounters` / `acts` / `events` / `ancients` の各 `.json`。
  `tools/extracted` はゲーム更新時に内容が変わるため、card-type-extractor が抽出時に各バージョンフォルダへ**生のままコピー**して版を固定する。
  読み込みは各サービス（`KeywordDatabaseService` / `EncounterDatabaseService` / `AncientDatabaseService` / `CardDatabaseService`）が
  `ResourceResolver.ResolveVersioned(asm, "localization.{lang}.{file}.json")` で最新版を解決する。
  （`cards.json` はバージョン管理の `card_descriptions.json` / `card_database.json` に移行済み）

**主要サービス**

| ファイル | 役割 |
|---|---|
| `Services/CardDatabaseService.cs` | カード名・説明・コスト・タイプ・シナジー判定。`_regentStarSpend` 等の HashSet はクラス初期化時に一括計算される |
| `Services/EncounterDatabaseService.cs` | エンカウンター・アクト名の EN/JP ルックアップ |
| `Services/DescriptionFormatter.cs` | `[gold]...[/gold]` 等の BBタグと `{Var:format}` テンプレートを除去・解決 |
| `CharacterMechanics.cs` | キャラクター × メカニクスのフィルタ定義（`Func<string, bool>` の配列）。CardBrowser のサイドバー構造と 1:1 対応 |

### データの流れ（共通パターン）

ローカライゼーションデータの読み込みは全サービスで同じパターン：
1. `Assembly.GetExecutingAssembly().GetManifestResourceNames()` でリソース名を検索
2. `GetManifestResourceStream()` でストリームを取得して `JsonDocument.Parse()`
3. `key.EndsWith(suffix)` によるマッチングを使用（LogicalName 付き埋め込みを含む）

バージョン別フォルダ（`Resources/v{version}/`）の JSON は全バージョンが埋め込まれるため、
`Services/ResourceResolver.cs` の `ResolveVersioned()` で `.Resources.v*` のうち**最大バージョン**を選ぶ。
これにより複数バージョン間および同名ローカライズ埋め込み（例 `card_keywords.json`）との衝突を回避する。

### card-type-extractor — メタデータ生成ツール

ゲームの `sts2.dll` の IL を `System.Reflection.Metadata` で直接解析する。

検出対象：
- カードコンストラクタの `ldc.i4` シーケンス → コスト・タイプ・レアリティ
- `get_HasStarCostX` が `ldc.i4.1 + ret`（2バイト）→ X スターコスト
- `get_CanonicalStarCost` が正の整数を返す → 固定スターコスト（1★・2★ 等）
- `get_CanonicalVars` → ダメージ・ブロックのデフォルト値

出力先はすべて `StS2Shared/Resources/`。extractor 実行後に `StS2Shared` を再ビルドすることで埋め込みリソースが更新される。

### StS2CardBrowser — カードブラウザ

- `CardBrowserForm.cs` が `CharacterMechanics.All` を読み込んでサイドバーボタンを動的生成
- キャラクター選択時はキャラクター帰属フィルタ、メカニクス選択時はメカニクスフィルタに切り替わる（キャラクターフィルタを置換する）
- サムネイル画像は `tools/extracted/images/card_portraits_png/{character}/{id}.png` から読み込み
- コスト表示は `GetCardCost()` が返す文字列（"0"〜"3+"・"X"・"-"）。スターコストは現在エネルギーコストと同じ数値で表示される（区別なし）

### StS2SiteBuilder — 静的サイトジェネレータ

ビルドは「生成」ボタンまたは `dotnet run --project StS2SiteBuilder` で実行。

**モンスター GIF アニメーション生成（ビルド時自動）：**
- `--build` 実行時に `tools/extracted/animations/monsters/` から Spine アニメーションを読み込み、
  idle アニメーションの GIF（192×192、10fps）を自動生成して `tools/extracted/images/monsters/` にキャッシュする。
- キャッシュ済みの GIF はスキップされる（`.skel.import` より新しければ再生成不要）。
- PNG スナップショット（256×256）は以前と同様に同ディレクトリに残る。

エンカウンター→モンスターの対応 `encounter_monsters.json`、モンスターの EN/JA 名 `monster_names.json`、
Act→イベントの対応 `event_acts.json` は、card-type-extractor が DLL のモデルクラス
（`Models.Encounters` / `Models.Monsters` / `Models.Acts`）とローカライズから
バージョンフォルダ（`Resources/{version}/`）へ生成する（`ResourceResolver` で最新を解決）。
モンスターは**モデル粒度**の ID 空間（例 `bowlbug_rock`）で、`monster_names` と `encounter_monsters` で共通。
画像/ページは ID をそのまま使うため、Spine フォルダがある ID のみ画像表示される。
Spine レンダリングには `SpineLoader.cs` / `SpineRenderer.cs`（StS2SiteBuilder 内）と
`SpineRuntime` プロジェクト（spine-csharp の pure C# ランタイム）を使用する。

**デプロイ（rsync でレンタルサーバーへ転送）：**

前提条件（初回のみ）：
- SSH 公開鍵をサーバーに登録済みであること
- `deploy.sh` 冒頭の `SERVER` / `REMOTE_PATH` / `SSH_PORT` を自分の環境に編集済みであること

```powershell
# 1. サイトを生成
dotnet run --project StS2SiteBuilder -- --build

# 2. Git Bash から差分転送
bash deploy.sh

# 転送前に確認したい場合
bash deploy.sh --dry-run
```

### ローカライゼーションの構造（ancients.json）

Ancient NPC のデータは `tools/extracted/localization/{eng,jpn}/ancients.json` に格納され、キー構造は：
```
{ANCIENT_ID}.talk.{CHARACTER}.{PAGE}.{MODE}  — 会話テキスト
{ANCIENT_ID}.pages.{PAGE}.description        — ページ説明
{ANCIENT_ID}.pages.{PAGE}.options.{OPT}.title/description  — 選択肢
```

カード授与は `OPTION_POOL_*` 系のキーで表現される（例: `OROBAS.pages.INITIAL.options.OPTION_POOL_3_LOCKED`）。
