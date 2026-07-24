# SpireScope ライブキャプチャ表示機能 仕様書

最終更新: 2026-06-23 / 対象バージョン: ゲーム v0.107.1 / ステータス: 設計確定（実装は別タスク）

## 1. 目的・背景

SpireScope（セーブデータビューア）に、**プレイ中のゲーム画面をキャプチャして関連情報を表示するフォーム**を追加する。

- StS2Capture で実装・実機確認済みの **カード提示画面検出**（キャラ別枠色 ＋ colorless）と
  **ショップ検出**（レリック/ポーション）を流用する。
- 進行中セーブ `current_run.save` の現在状態（キャラ・デッキ・レリック・Act・HP・現在ノード種別）と統合表示する。
- 検出したカード/レリック等から、StS2SiteBuilder 生成サイトや外部 Wiki の **情報ページへリンク**する
  （設定で URL テンプレートを複数定義）。
- 将来機能として **イベント／戦闘（エンカウンター）検出** も視野に入れた拡張可能な設計にする。

## 2. スコープ

- **v1（本仕様の主対象）**
  1. キャプチャ基盤の共有ライブラリ化
  2. カード提示／ショップ検出の SpireScope 統合
  3. セーブ連携による現在状態表示
  4. URL テンプレートによる情報ページリンク
- **将来（設計メモのみ・実装は別タスク）**
  - イベント画面検出、戦闘画面のモンスター検出、画面種別の自動分類

## 3. アーキテクチャ（再利用方式）

新規クラスライブラリ **`StS2Capture.Core`（TFM `net10.0-windows10.0.19041.0`）** を作り、capture/recognition を集約する。
`StS2Capture`(exe) と `SpireScope` の双方がこれを参照する。**SpireScope の TFM を `net10.0-windows` →
`net10.0-windows10.0.19041.0` に引き上げる**ことで、WGC キャプチャ＋OCR＋テンプレ照合＋ショップ検出をフル再利用する。

### プロジェクト構成（変更後）

| プロジェクト | TFM | 役割 |
|---|---|---|
| `StS2Shared` | `net10.0` | 既存。データ／サービス（`CardDatabaseService`/`SaveDataService`/`RelicImageService`/`PotionImageService` ほか） |
| `StS2Capture.Core`（新規） | `net10.0-windows10.0.19041.0` | キャプチャ＋認識ロジック（下記クラス群）。`StS2Shared` を参照 |
| `StS2Capture`(exe) | `net10.0-windows10.0.19041.0` | 既存の試験 UI（`MainForm`）のみ残し `Core` を参照 |
| `SpireScope` | `net10.0-windows10.0.19041.0`（引上げ） | `Core` ＋ `StS2Shared` を参照。新フォームを追加 |

### `StS2Capture.Core` へ移動するクラス

- `Capture/`：`IFrameSource` / `WgcFrameSource`(WGC) / `GdiFrameSource`(GDI) / `GameWindowLocator` / `WindowClientArea`
- `Recognition/`：`ICardRecognizer` / `OcrCardRecognizer`(OCR) / `TemplateCardRecognizer` / `ShopItemRecognizer` /
  `CardRegionDetector` / `FrameColorProfile` / `HsvHistogram` / `ImageOps` / `CardNameIndex` /
  `RecognizedCard` / `RecognitionResult` / `OcrTextSpan`
- ルート：`CaptureLoop`

### 再利用する主要 API

- `CaptureLoop`：`Start()` / `Stop()` / `CaptureOnce()` / `CaptureRawFrame()`、`event Action<Result> Updated`、
  `Result(string Status, bool IsCardScreen, IReadOnlyList<RecognizedCard> Cards, IReadOnlyList<OcrTextSpan> TextSpans,
  IReadOnlyList<Rectangle> CardBoxes, IReadOnlyList<Rectangle> TitleBands, Bitmap? Preview)`
- `ShopItemRecognizer.Detect(Bitmap frame, Rectangle client) → Result(bool IsShop, IReadOnlyList<Item> Items)`
- `WindowClientArea.Resolve(IntPtr hwnd, int w, int h) → Rectangle`（クライアント領域＝タイトルバー/枠除外、全画面対応）

## 4. 機能仕様

### 4.1 新フォーム `LiveCaptureForm`

- Form1（メイン）から既存の子フォーム定石で開く：Form1 にフィールド追加、サイドパネルにボタン追加、
  `WindowSettingsService` で位置・サイズを永続化、`AppLanguage.IsJapanese` で EN/JP 切替。
- 画面構成（案）
  - 上部：操作バー（自動監視 ON/OFF、取得手段 WGC/GDI、手動キャプチャ）
  - 左：**現在状態パネル**（セーブ由来。4.3）
  - 中央：**キャプチャプレビュー**（検出枠オーバーレイ。カード枠＝緑／タイトル帯＝赤／ショップスロット＝緑・橙）
  - 右：**検出結果リスト**（名称・確信度・情報ページリンク。4.4）

### 4.2 キャプチャ＆画面種別判定

- `CaptureLoop` をポーリング（既定 ~800ms）。`GameWindowLocator.Find()` ＋ `WindowClientArea.Resolve` で
  クライアント領域を確定（座標はクライアント相対のため解像度・タイトルバー有無に非依存）。
- v1 の判定
  - **カード提示画面**：card 認識（`TemplateCardRecognizer` 主、`OcrCardRecognizer` 併用可）。相異なるカード ≥2 で提示画面。
  - **ショップ画面**：`ShopItemRecognizer.Detect`（固定スロット probe ＋ 強一致ゲート）。
  - 両者を probe し、強一致した方を「現在画面」として表示。両者非該当なら「待機」。
- 補助：セーブの現在ノード種別（4.3）と整合チェック（例：`shop` ノードならショップ検出を優先）。

### 4.3 セーブ連携（現在状態）

`SaveDataService.Load(SaveDataService.GetDefaultSavePath())` を mtime 監視で読み込み（既存方式）。
`RunSaveData` から以下を表示する。

| 区分 | フィールド | 表示 |
|---|---|---|
| プレイヤー | `Players[0].CharacterId` | キャラ表示名（`CardDatabaseService.GetName` 等） |
| プレイヤー | `CurrentHp`/`MaxHp`、`Gold`、`MaxEnergy` | HP・ゴールド・エネルギー |
| プレイヤー | `Deck`（`List<CardData>`） | デッキ一覧 |
| プレイヤー | `Relics`（`List<RelicData>`） | 所有レリック（「所有」表示。ショップのオファー品＝未所有と区別） |
| ラン | `Ascension`、`CurrentActIndex`（0=Act1…2=Act3） | アセンション・現在 Act |
| ラン | `Acts[CurrentActIndex].Rooms`（`BossId`/`SecondBossId`/`EliteEncounterIds` ほか） | 当 Act のボス・エリート候補 |
| 進行 | `MapPointHistory[][]`（`MapPointEntry.MapPointType`） | 訪問ノード／**現在・直近のノード種別** |

`MapPointType` の値：`monster`（通常戦闘）/ `elite` / `boss` / `shop` / `treasure`（宝箱・報酬）/ `rest_site`（焚き火）。
→ 現在地の種別表示および画面種別判定の補助に使用。

### 4.4 検出結果の表示とリンク

- 検出した各エンティティ（v1：カード／レリック／ポーション。将来：モンスター／イベント）について、
  **名称（EN/JP）＋確信度＋設定済み URL テンプレートごとのリンク**を表示する。
- リンクは既定ブラウザで開く（`Process.Start(new ProcessStartInfo(url){UseShellExecute=true})`）。フォーム内 WebView は非対象。
- 僅差で複数候補がある場合（ショップの同色ペア等）は全候補を併記する（`ShopItemRecognizer` の既存仕様）。

## 5. ID → 名称 → URL の対応（リンク生成の基礎）

| 種別 | ID 例 | 名称取得 | SiteBuilder ページ規則（参考） |
|---|---|---|---|
| カード | `CARD.BASH` | `CardDatabaseService.GetName(id, japanese)` | `cards/{cardClass}/{rawIdLower}.html`（cardClass=ironclad/silent/defect/necrobinder/regent/shared）|
| レリック | `AKABEKO` | `CardDatabaseService.GetRelicTitle(id, japanese)` | `relics/{id}.html` |
| ポーション | `FIRE_POTION` | localization（potions）| `potions` ページ（個別ページ有無は実装時に確認）|
| モンスター | `bowlbug_rock` | `monster_names`（en/ja）| `monsters/{dirName}.html` |
| イベント | `ABYSSAL_BATHS` | `CardDatabaseService.GetEventTitle(id, japanese)` | `events/{id}.html` |
| エンカウンター | `EXORDIUM_TOUGH_FIGHT` | `EncounterDatabaseService.GetEncounterName(id, japanese)` | `encounters/{id}.html` |

補助変換（旧 StS2SiteBuilder の `RawId`/`GetCardDir` と同一規則。サイト生成コードは撤去済み＝git 履歴参照。現行実装は `SiteLinkService`）：
- `rawId` = ID の `.` 以降（`CARD.BASH` → `BASH`）
- `cardClass` = `CardDatabaseService.GetCardCharacter(id)` を {ironclad, silent, defect, necrobinder, regent, shared} に対応付け

## 6. URL テンプレート仕様（設定で複数定義）

ユーザが **任意個のテンプレート** を定義でき、検出エンティティごとに各テンプレートのリンクを生成する。

### テンプレート定義

配列要素 = `{ label（表示名）, type（対象種別）, template（テンプレ文字列）}`。
`type` ∈ `card` / `relic` / `potion` / `monster` / `event` / `encounter` / `any`。

### 差し込みトークン

- **コア3種**
  - `{id}` … ID 名（例 `CARD.BASH`）
  - `{en}` … 英語名（例 `Bash`）
  - `{jp}` … 日本語名（例 `強打`）
- **派生／変換トークン**（公式サイト等のパス生成に必要なため補助提供）
  - `{idraw}` … 接頭辞除去 ID（`BASH`）
  - `{idrawlower}` … 同・小文字（`bash`）
  - `{cardclass}` … カードのキャラ別ディレクトリ（`ironclad` 等。カード以外は空文字）
- URL に使えない文字（日本語等）は **自動で URL エンコード** する。

### 例

| label | type | template | 生成例 |
|---|---|---|---|
| 公式サイト | `card` | `https://lichtenlab.com/sts2/cards/{cardclass}/{idraw}.html` | `https://lichtenlab.com/sts2/cards/ironclad/BASH.html` |
| 公式（小文字運用） | `card` | `https://lichtenlab.com/sts2/cards/{cardclass}/{idrawlower}.html` | `.../cards/ironclad/bash.html` |
| Wiki | `any` | `https://wikiwiki.jp/sts2/{jp}` | `https://wikiwiki.jp/sts2/%E5%BC%B7%E6%89%93`（`強打` をエンコード）|

### 実装

`SiteLinkService`（`StS2Shared` または `StS2Capture.Core`）を新設：`(種別, id) → 名称・派生トークン解決 → テンプレ展開`。
カードの `cardClass` / `rawId` 変換は SiteBuilder の `RawId` / `GetCardDir` 規則に合わせる
（重複定義を避けるため、将来 SiteBuilder と共通化を検討）。

## 7. 設定／永続化

既存 `WindowSettingsService` / `AppSettings`（SpireScope）に項目を追加する。

- `UrlTemplates` … 上記テンプレート配列
- ライブキャプチャ窓の位置・サイズ、取得手段（WGC/GDI）既定、自動監視 ON/OFF、ポーリング間隔
- 設定 UI：テンプレートの追加／編集／削除（簡易グリッド）

## 8. 将来機能（設計メモのみ）

- **イベント検出**：`EventImageService` ＋ `events_png`（55 枚・実在）をテンプレ照合（ショップと同じ
  「透過余白トリム＋背景合成＋HSV ヒストグラム」方式）。画面中央のイベント画像領域を probe。
  `event_acts.json`（`EventActService`）で現 Act のイベントに **候補を絞り込み**、精度を上げる。
- **戦闘検出**：`encounter_monsters.json` / `MonsterDatabaseService` ＋ monsters 画像（121 枚＋GIF）。
  画面上部のモンスター帯を検出し、`encounter_acts.json`（`EncounterActService`）＋現 Act・難易度で候補を絞って
  モンスター照合 → エンカウンター推定。
- **画面種別分類**：セーブの `MapPointType` を一次手がかりに、各画面の probe（card/shop/event/combat/rest）を切り替える。

## 9. 非対象・制約

- WGC は GPU 描画（Vulkan/DX）で黒画面になる可能性があり実機要確認。不可なら GDI フォールバック（既存実装あり）。
- ショップ等のスロット座標はクライアント相対で較正済みだが、ゲーム UI レイアウト変更時は要再較正。
- 公開サイト URL はユーザ設定（既定は空またはローカル dist）。リンクはブラウザ起動のみ（フォーム内 WebView は非対象）。

## 10. 実装計画（別タスク）

1. `StS2Capture.Core` 抽出 ＋ 両プロジェクト参照、SpireScope TFM 引上げ、ソリューション全体ビルド。
2. `LiveCaptureForm`：キャプチャ＋カード/ショップ検出＋プレビュー（StS2Capture の `MainForm` 相当を移植・整理）。
3. セーブ現在状態パネルの統合。
4. `SiteLinkService` ＋ URL テンプレート設定 ＋ 結果リストのリンク。
5. （将来）イベント／戦闘検出。

## 11. 検証

- `dotnet build`（ソリューション全体・TFM 引上げ後、0 エラー）。
- 実機：カード提示画面・ショップで検出が StS2Capture と同等に機能。セーブ現在状態が一致。
- リンク：カード/レリックから設定テンプレ通りの URL がブラウザで開く（公式・Wiki 双方の例）。
- 非回帰：SpireScope 既存フォーム、StS2Capture 単体動作。

## 付録: 参照したコード/資産

- StS2Capture：`CaptureLoop.cs` / `Capture/*` / `Recognition/*`（`ShopItemRecognizer`/`HsvHistogram`/`FrameColorProfile` ほか）
- StS2Shared：`Models/SaveData.cs`（`RunSaveData`）/ `Services/SaveDataService.cs` /
  `CardDatabaseService.cs`（`GetName`/`GetRelicTitle`/`GetEventTitle`/`GetCardCharacter`/`GetAll*Ids`）/
  `RelicImageService.cs` / `PotionImageService.cs` / `EventImageService.cs` / `EventActService.cs` /
  `EncounterDatabaseService.cs` / `EncounterActService.cs` / `MonsterDatabaseService.cs` / `ResourceResolver.cs`
- 旧 StS2SiteBuilder：`SiteBuilderCore.cs`（`RawId`/`GetCardDir`/`MecFileName`、各 `Build*Page` と href 生成、`NodeBadge`）
  ※ サイト生成コードは撤去済み（git 履歴参照）。URL 規則の現行実装は `SiteLinkService` / `UrlTemplateService`
- SpireScope：`Form1.cs`（子フォーム定石・`DisplayData`）/ `Services/AppLanguage.cs` / `Services/WindowSettingsService.cs`
- 画像資産：`tools/extracted/images/`（`relics_png` 295・`potions_png` 64・`events_png` 55・`monsters` 121＋GIF）
