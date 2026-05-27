# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```powershell
dotnet build                                  # ソリューション全体
dotnet run --project StS2Toys                 # セーブデータビューア
dotnet run --project StS2CardBrowser          # カードブラウザ
dotnet run --project StS2EventBrowser         # イベントブラウザ
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

画像は BC7 圧縮テクスチャ（`.ctex`）で、StS2EventBrowser が初回アクセス時に PNG 変換してキャッシュする。

## Architecture

本リポジトリは .NET 10 / Windows Forms のデスクトップアプリ群で、Slay the Spire 2 のゲームデータを参照・閲覧する。

### プロジェクト構成

| プロジェクト | 役割 |
|---|---|
| `StS2Shared` | 全アプリが参照する共有ライブラリ（サービス・メカニクス定義） |
| `StS2Toys` | セーブデータビューア（デッキ・レリック表示） |
| `StS2CardBrowser` | カードブラウザ（キャラクター・メカニクスフィルタ付き） |
| `StS2EventBrowser` | イベントブラウザ（テキスト・画像表示） |
| `card-type-extractor` | ゲーム DLL の IL を解析してカードメタデータ JSON を生成するCLIツール |

### StS2Shared — 共有ライブラリ

`StS2Shared` は全アプリが `ProjectReference` で参照する。埋め込みリソースとして以下を保持する：

**`Resources/*.json`（手動管理 or card-type-extractor 生成）**
- `card_database.json` — 手動管理の EN/JP 名前オーバーライド
- `card_types.json`, `card_costs.json`, `card_rarities.json`, `card_characters.json` — ゲーム DLL から抽出
- `card_star_costs.json` — スターコストを持つカードの ID リスト（`get_CanonicalStarCost > 0` または `get_HasStarCostX` が true のもの）
- `card_stats.json` — カードのキャノニカル変数（ダメージ・ブロック値など）

**ローカライゼーション JSON（`tools/extracted/` から埋め込み）**
- `localization/{eng,jpn}/{cards,relics,enchantments,encounters,acts}.json`

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

### StS2EventBrowser — イベントブラウザ

- `events.json` を `{EVENTID}.{subkey}` 形式でパースし、`pages.INITIAL.description` と `pages.INITIAL.options.*` から初期ページを構築
- イベント画像は `tools/extracted/images/events/{id}.png.import` → `.ctex` パス解決 → BC7 デコードまたは WebP デコードで PNG 変換してキャッシュ
- 画像キャッシュ先: `tools/extracted/images/events_png/`

### StS2MonsterBrowser — モンスターアニメーションビューア

- Spine スケルタルアニメーション（tools/extracted/animations/monsters/{name}/）を読み込んで表示
- 「全スナップショット生成」ボタンで全モンスターの idle ポーズを 256×256 PNG として `tools/extracted/images/monsters/` に出力する

### StS2SiteBuilder — 静的サイトジェネレータ

ビルドは「生成」ボタンまたは `dotnet run --project StS2SiteBuilder` で実行。

**エンカウンターページのモンスター画像ワークフロー（git 管理外のため要手順）：**
1. `StS2MonsterBrowser` を起動して「全スナップショット生成」をクリック
   → `tools/extracted/images/monsters/*.png` が生成される
2. `StS2SiteBuilder` でビルドを実行
   → `dist/images/monsters/` にコピーされ、エンカウンターページにモンスターグリッドが表示される

モンスター画像が未生成の場合はエンカウンターページに `?` プレースホルダーが表示される。
エンカウンター→モンスターの対応は `StS2Shared/Resources/encounter_monsters.json` で管理。
モンスターの EN/JA 名は `StS2Shared/Resources/monster_names.json` で管理。

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
