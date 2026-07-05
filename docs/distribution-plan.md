# StS2Toys 一般プレイヤー向け配布計画

Slay the Spire 2 をインストール済みのプレイヤーが、**開発環境なし・ゲームアセットの再配布なし**で
StS2Toys（セーブデータビューア）を利用できるようにするための計画書。

- 作成日: 2026-07-05
- ステータス: 計画（未着手）

---

## 1. 目的とゴール

### 現状の問題

StS2Toys は現在、開発者本人の環境でのみ動作する。他のプレイヤーに使ってもらうには
以下の手順が必要で、非開発者には高いハードルになっている：

1. .NET SDK（Visual Studio 等）のインストールとソースからのビルド
2. GodotPCKExplorer を手動で入手し、ゲームの `.pck` から約 1.8 GB を全展開
3. `ctex-to-png` を手動実行して `.ctex` → PNG 変換

### ゴール

> **「zip をダウンロード → 展開 → exe を起動」だけで使える。**
> 初回起動時にアプリ自身がユーザーの Slay the Spire 2 インストールから
> 必要なアセットだけを自動抽出する。ゲームアセット（画像・テキスト）は一切再配布しない。

### 非ゴール

- StS2SiteBuilder / card-type-extractor / ctex-to-png（CLI）の配布 — 開発者用ツールとして対象外
- インストーラ（MSI 等）や winget / Microsoft Store 対応 — 将来検討
- Steam 以外のプラットフォーム（現状 StS2 は Steam のみ）

---

## 2. 配布方針の決定事項

| 論点 | 決定 | 理由 |
|---|---|---|
| 配布対象 | **StS2Toys のみ** | 一般プレイヤーに価値があるのはセーブビューア。他は開発者用 |
| 配布形態 | **GitHub Releases の zip**（self-contained、単一 exe） | インストーラ不要で最も手軽。リポジトリは公開済みで Releases と親和 |
| アセット取得 | **アプリ内蔵の初回セットアップウィザード** | ユーザー操作を最小化。手動ツール実行を完全排除 |
| ローカライズテキスト | **セットアップ時に .pck から抽出**（配布物に含めない） | ゲームテキストも著作物。画像と同じ扱いに統一する厳密方式 |
| PCK 展開 | **自前 C# PCK リーダーで選択的抽出** | 1.8 GB 全展開を回避（必要分は数百 MB 以下）。外部 exe 同梱・バージョン追従が不要 |
| 動作要件 | Windows 10 1903+ / x64 | StS2Capture.Core の WinRT（画面キャプチャ・OCR）依存による |

---

## 3. 法的整理

### 再配布しないもの（ユーザーの手元で抽出する）

- 画像アセット（カードポートレート、レリック/カードアトラス、アイコン、モンスター画像等の `.ctex`）
- ローカライズテキスト（`localization/{eng,jpn}/*.json` — カード名・説明文・イベント文 等）
- `.pck` の内容物すべて

### 配布するもの

- 自作コード（本リポジトリ、MIT ライセンス — `LICENSE` 設定済み）
- IL 解析で抽出したメタデータ JSON（`card_types.json`・`card_costs.json`・`card_stats.json` 等）
  — コスト・タイプ・数値・ID といった**事実データ**であり、表現（テキスト・画像）を含まない
- 手動管理の独自コンテンツ（`monster_move_patterns.json` の自然文アノテーション等 — 自作テキスト）

### 対応が必要な項目

1. **配布バイナリからゲームテキストを除外する**（→ Phase 4）。
   現在 `StS2Shared` の埋め込みリソースに `card_database.json` / `card_descriptions.json` /
   `potion_database.json` / `potion_descriptions*.json` / `Resources/{version}/localization/**` 等の
   ゲームテキスト由来 JSON が含まれている。
2. **公開リポジトリ上の埋め込み JSON の扱い**（検討事項・配布とは独立）。
   上記 JSON はリポジトリにもコミットされている。厳密にはこれも再配布に当たるため、
   将来的に「リポジトリからも分離し、開発者はローカル抽出物から生成する」構成への移行を検討する。
   配布計画のブロッカーにはしない（配布物から除外されていれば配布自体の問題はない）。
3. **免責の明記**。README・配布 zip 同梱の README・アプリの About に
   「非公式ファンツールであり Mega Crit と無関係。Slay the Spire 2 の著作権は Mega Crit に帰属」を記載する。
4. **サードパーティライセンス表記**。`THIRD-PARTY-NOTICES.txt` を zip に同梱：
   - BCnEncoder.NET（MIT）、Pfim（MIT）、SixLabors.ImageSharp（Six Labors Split License
     — 本リポジトリは MIT の OSS なので無償条件を満たす）
   - PCK フォーマットの参照実装として GodotPCKExplorer（MIT）のソースを参考にする場合はクレジット記載

---

## 4. アーキテクチャ変更計画

### 現状の課題（コード上の障害）

| 課題 | 現状 |
|---|---|
| アセット解決 | exe の親を遡って `tools/extracted` を探すウォークアップ方式が約 8 箇所に分散（`StS2Toys/DeckOverviewForm.cs:674`、`StS2Toys/Services/RelicImageService.cs:89`、`CardAtlasService.cs` / `EnchantmentIconService.cs` / `MonsterImageService.cs`、`StS2Capture.Core` の認識系 4 ファイル）。配布 exe の親に `tools/extracted` は存在しないため画像が出ない |
| PCK 展開 | GodotPCKExplorer の手動実行のみ（コードからの呼び出しなし） |
| ctex 変換 | `ctex-to-png` CLI の手動実行が前提。ただし実装は BCnEncoder.NET + ImageSharp の完全マネージド（`ctex-to-png/Program.cs:279-350`）で移設可能 |
| Steam パス検出 | 未実装（card-type-extractor は `C:\Program Files (x86)\Steam\...` をハードコード） |
| セーブ読み込み | `%AppData%\SlayTheSpire2\steam\{steamId}\profile1\saves\`（`StS2Shared/Services/SaveDataService.cs:20-34`）— Steam ライブラリ位置と独立なので**無改修で動く** |

### Phase 1: アセット解決の一元化（`AssetLocator`）

`StS2Shared` に `AssetLocator`（仮称）を新設し、全アプリのアセットルート解決を一本化する。

解決順序：

1. **開発モード**: 従来どおり exe の親を遡って `tools/extracted` を探す（開発環境では現状の挙動を完全維持）
2. **配布モード**: `%LocalAppData%\StS2Toys\assets\{gameVersion}\` を探す
3. どちらも無ければ「未セットアップ」を返し、呼び出し側（StS2Toys）がセットアップウィザードを起動

既存のウォークアップ実装（`FindExtractedDir` / `GetPortraitsDir` / `FindSpritesDir` /
`FindToolsRoot` 等）をすべて `AssetLocator` 呼び出しに置換する。
抽出先のディレクトリ構造は `tools/extracted` と同一レイアウトにし、置換後のパス組み立てコードを変えずに済ませる。

### Phase 2: 抽出エンジンの内製化（新ライブラリ `StS2Shared.Assets` 仮称）

配布アプリと開発 CLI の双方から使う共有ライブラリとして実装する。

**(a) 自前 PCK リーダー**
- Godot 4.5 系の PCK（フォーマット v3）のインデックスを読み、**必要なパスだけ**を抽出する
- フォーマットは公開情報＋GodotPCKExplorer（MIT）のソースを参照実装とする
  - ヘッダ: `GDPC` マジック + フォーマットバージョン + エンジンバージョン + フラグ
  - ファイルインデックス: パス長 + `res://` パス + オフセット + サイズ + MD5 + フラグ
- StS2 の `.pck` は非暗号化（GodotPCKExplorer で展開できている実績あり）。
  暗号化フラグが立っていた場合は明確なエラーメッセージで中断する
- 実装後、GodotPCKExplorer の展開結果とバイナリ一致することを検証する

**(b) ctex → PNG 変換の移設**
- `ctex-to-png/Program.cs` の `ConvertCtex` / `DecodeBc` / `LoadWebP` / `ParseImportCtexPath`
  （`Program.cs:279-359`）をライブラリへ移設。CLI は薄いラッパーとして残す
- 依存: BCnEncoder.NET + SixLabors.ImageSharp（いずれもマネージドで self-contained publish に支障なし）

**(c) Steam インストールパス自動検出**
- レジストリ `HKCU\Software\Valve\Steam` の `SteamPath` → `{SteamPath}/steamapps/libraryfolders.vdf`
  をパースして全ライブラリフォルダを列挙 → 各 `steamapps/common/Slay the Spire 2/SlayTheSpire2.pck` を探す
- 見つからなければフォルダ選択ダイアログにフォールバック（`.pck` または ゲームフォルダを指定）
- 検出したパスと `release_info.json`（DLL 隣）のゲームバージョンを設定ファイルに記録

### Phase 3: 初回セットアップウィザード（StS2Toys 内蔵）

起動時に `AssetLocator` が「未セットアップ」を返したら表示するモーダルフォーム。

**抽出対象マニフェスト**（StS2Toys が実行時に参照するもののみ。全展開はしない）:

| 対象 | .pck 内パス | 変換 |
|---|---|---|
| カードポートレート | `images/card_portraits/**`（`.png.import` → `.godot/imported/*.ctex`） | ctex→PNG（`card_portraits_png/` レイアウトで保存） |
| レリックアトラス | `.godot/imported/relic_atlas.png-*.bptc.ctex` + `images/atlases/relic_atlas.tpsheet` | そのまま（実行時 BC7 デコード済みの経路を維持） |
| カードアトラス | `images/atlases/card_atlas.sprites` ほか `CardAtlasService` 参照物 | そのまま |
| エンチャントアイコン | `EnchantmentIconService` 参照物 | 現行と同形式 |
| モンスター画像 | `MonsterImageService` 参照物 | 現行と同形式 |
| キャプチャ認識テンプレート | `StS2Capture.Core` の認識系が参照する画像 | 現行と同形式 |
| ローカライズ | `localization/{eng,jpn}/*.json` | そのままコピー |
| バージョン情報 | ゲームフォルダの `release_info.json` | コピー（再同期判定に使用） |

（実装時に各サービスの参照パスを網羅的に洗い出してマニフェストを確定する。
マニフェストはコード内の静的定義とし、ゲーム更新でパスが増えた場合はアプリ更新で追従する）

**ウィザードの流れ**:

1. Steam パス自動検出（失敗時は手動指定）
2. 抽出内容と保存先（`%LocalAppData%\StS2Toys\assets\{gameVersion}\`）の確認表示
3. 進捗バー付きで抽出・変換（バックグラウンドスレッド、キャンセル可能）
4. 完了 → メインフォームへ

**ゲーム更新時の再同期**:
- 起動時に `.pck` の更新日時 or `release_info.json` のバージョンを記録値と比較し、
  変化していたら「ゲームが更新されています。アセットを再抽出しますか？」を提示
- 旧バージョンのアセットフォルダは再抽出成功後に削除（ディスク節約）

### Phase 4: ゲームテキストの外部化

- 配布ビルドでは、ゲームテキスト由来の埋め込みリソース
  （`card_database.json` / `card_descriptions.json` / `potion_database.json` /
  `potion_descriptions*.json` / `Resources/{version}/localization/**` 等）を
  MSBuild プロパティ（例 `/p:ExcludeGameText=true`）で埋め込みから除外する
- 各サービス（`CardDatabaseService` / `KeywordDatabaseService` / `EncounterDatabaseService` 等）の
  リソース読み込みに「外部アセットストア（セットアップで抽出した localization JSON）優先 →
  埋め込みフォールバック」の 2 段解決を追加する
  - `card_database.json` / `card_descriptions.json` は元々ローカライズの `{ID}.title` / `{ID}.description`
    から生成しているため、外部読み込み時は抽出済み `cards.json` 等から同等の辞書を実行時に構築する
- 開発ビルドは現状の埋め込み動作を完全維持（差分は配布 publish 時のフラグのみ）
- IL 由来のメタデータ JSON（数値・ID）は埋め込みのまま配布する

### Phase 5: ビルド・リリースパイプライン

**publish 構成**:

```powershell
dotnet publish StS2Toys -c Release -r win-x64 --self-contained `
  -p:PublishSingleFile=true -p:ExcludeGameText=true `
  -p:IncludeNativeLibrariesForSelfExtract=true
```

- .NET 10（LTS、2025-11 GA）の self-contained なのでユーザー側のランタイムインストール不要
- 単一 exe + zip 同梱物: `README.txt`（使い方・免責）、`LICENSE`、`THIRD-PARTY-NOTICES.txt`

**GitHub Actions**:
- タグ（例 `v1.0.0`）push をトリガーに publish → zip 化 → GitHub Releases へ添付
- ワークフローは `windows-latest`、`actions/setup-dotnet` で .NET 10 SDK を導入

---

## 5. 想定ユーザー体験

### 初回

1. GitHub Releases から `StS2Toys-vX.Y.Z-win-x64.zip` をダウンロードして展開
2. `StS2Toys.exe` を起動（SmartScreen 警告が出た場合は「詳細情報 → 実行」— README に手順記載）
3. セットアップウィザードが Steam から Slay the Spire 2 を自動検出（見つからなければフォルダ指定）
4. 数分の抽出・変換（進捗表示）ののち、メイン画面が開く
5. 進行中のランがあればセーブが自動で読み込まれる（`%AppData%` 固定なので設定不要）

### ゲームアップデート後

- 起動時に更新を検知し、再抽出を促すダイアログを表示。「あとで」も選べる
  （その場合は旧アセットのまま動作し、新カード等の画像のみ欠ける）

### アプリアップデート

- 新 zip を上書き展開するだけ。アセットは `%LocalAppData%` 側にあるため再抽出は不要
  （マニフェスト拡張があった場合のみ差分抽出を提案）

---

## 6. リスクと未解決事項

| リスク | 影響 | 対応方針 |
|---|---|---|
| ゲームバージョンと埋め込みメタデータの不一致（ゲームだけ更新された） | 新カードのコスト・タイプ等が欠ける | 欠損 ID は「不明」表示でクラッシュさせない（既存のフォールバック方針を踏襲）。アプリ側の追従リリースで解消 |
| PCK フォーマットの将来変更（Godot エンジン更新） | セットアップが失敗する | フォーマットバージョンを検査し、未対応版は明確なエラー＋issue 誘導。GodotPCKExplorer の追従を参照 |
| `.pck` の暗号化が将来有効になる | 抽出不能 | 現状非暗号化。有効化されたら方針再検討（その場合は全ツールが影響を受ける） |
| SmartScreen / ウイルス対策の誤検知（未署名 exe） | 初回起動の離脱 | README に回避手順を記載。利用者が増えたらコード署名（費用要）を検討 |
| Mega Crit からの削除要請 | 配布停止 | アセット非再配布・免責明記でリスク最小化。要請があれば従う |
| セットアップの所要時間・ディスク使用 | UX 低下 | 選択的抽出で全展開（1.8 GB）を回避。想定は数百 MB 以下・数分 |
| 公開リポジトリ上のゲームテキスト JSON（法的整理 #2） | 潜在的な著作権懸念 | 配布とは独立の検討事項として継続。リポジトリからの分離を将来課題とする |

---

## 7. マイルストーン

| # | 内容 | 依存 | 目安 |
|---|---|---|---|
| M1 | Phase 1: `AssetLocator` 導入・既存 8 箇所の置換（開発環境で回帰なしを確認） | なし | 小 |
| M2 | Phase 2: PCK リーダー + ctex 変換移設 + Steam 検出（CLI から検証可能に） | M1 | 中〜大（PCK リーダーの検証含む） |
| M3 | Phase 3: セットアップウィザード（マニフェスト確定含む） | M2 | 中 |
| M4 | Phase 4: ゲームテキスト外部化（2 段解決 + publish フラグ） | M3 | 中 |
| M5 | Phase 5: GitHub Actions + Releases、README・免責・NOTICES 整備 | M4 | 小 |
| M6 | クリーン環境（別 PC / VM）での E2E 検証 → 初回リリース | M5 | 小 |

**検証方法（M6）**: 開発環境の無いクリーンな Windows マシンに Steam + StS2 のみを入れ、
zip 展開 → 起動 → ウィザード完走 → カード/レリック画像・日本語テキスト表示・セーブ読み込みを確認する。
