# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```powershell
dotnet build                                  # ソリューション全体
dotnet run --project StS2Toys                 # セーブデータビューア
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
| `StS2Shared.Assets` | `.pck` 読み取り（`PckReader`/`CtexDecoder`）と配布セットアップの抽出エンジン（`AssetExtractor`/`AssetSetup`/`SteamLocator`/`LocTextDeriver`） |
| `StS2Shared.Spine` | Spine 描画の共有ライブラリ（`SpineLoader`/`SpineRenderer`/`CreatureVisual`。`IAssetSource` でディスク/pck 両対応、`MonsterPngRenderer`）。SiteBuilder と配布セットアップが共用 |
| `StS2Toys` | セーブデータビューア（デッキ・レリック・敵情報・HP変動・ポーション確率・ライブキャプチャ） |
| `StS2Capture.Core` | ライブ画面キャプチャ（WGC）と画面認識（カード選択/ショップ/エンシェント。固定矩形＋HSV 照合、OCR はエンシェントのみ） |
| `StS2Capture` | キャプチャ検証用の単体アプリ（試験用） |
| `StS2SiteBuilder` | 静的サイトジェネレータ |
| `card-type-extractor` | ゲーム DLL の IL を解析してカードメタデータ JSON を生成するCLIツール |
| `ctex-to-png` | `.ctex`→PNG 変換 CLI（relics/events/ancients/potions ほか、extract-pck サブコマンドあり） |
| `SpineRuntime` | spine-csharp の pure C# ランタイム（データ構造のみ、描画なし） |

### StS2Shared — 共有ライブラリ

`StS2Shared` は全アプリが `ProjectReference` で参照する。埋め込みリソースとして以下を保持する：

**`Resources/*.json`（手動管理 or card-type-extractor 生成）**
- `card_database.json` — カード・レリックの EN/JP 表示名。ローカライズの `{ID}.title` から card-type-extractor がバージョンフォルダ（`Resources/{version}/`）へ生成
- `card_descriptions.json` — カードの EN/JP 説明文（生テキスト＝`[gold]`タグや`{Var}`テンプレート保持）。ローカライズの `{ID}.description` から生成。`GetDescription` とシナジー判定の読み元（cards.json 埋め込みから移行）
- `card_types.json`, `card_costs.json`, `card_rarities.json`, `card_characters.json` — ゲーム DLL から抽出
- `character_colors.json` — キャラクター（`IRONCLAD` 等）の色。各 `CharacterModel`（`Models.Characters`）の Color プロパティと
  `Helpers.StsColors` から抽出。`name`/`nameColor`（識別色 red/green/blue/purple/orange の hex）と UI パレット
  （`mapDrawingColor`/`dialogueColor`/`energyOutlineColor`/`targetingLineColor`）を持つ。`CharacterColorService` が読み、
  SiteBuilder のキャラアクセント色（`CharData.Accent` = 白文字背景でも可読な `MapDrawingColor`）に使う
- `card_star_costs.json` — スターコストを持つカードの ID リスト（`get_CanonicalStarCost > 0` または `get_HasStarCostX` が true のもの）
- `card_upgraded_costs.json` — アップグレードでコストが変わるカードのみ収録（`OnUpgrade` の `EnergyCost.UpgradeBy/To` から抽出）。`CardDatabaseService.GetUpgradedCost(Value)` で参照
- `card_stats.json` — カードのキャノニカル変数（ダメージ・ブロック値など）
- `card_images.json` — カード ID → 画像のソース相対パス（`card_portraits_png/` 基準、例 `silent/abrasive.png`）。
  extractor が実ファイルをスキャンして生成。`Services/CardImageService.cs` で参照し、SiteBuilder/Toys 双方の画像解決を一元化
- `relic_images.json` — レリック ID（接頭辞なし大文字、例 `AKABEKO`）→ 画像のソース相対パス（`relics_png/` 基準、例 `akabeko.png`・`beta/belt_buckle.png`）。
  extractor が `tools/extracted/images/relics/` の `.png.import` をスキャンして生成（`beta/` 含む）。`Services/RelicImageService.cs` で参照。
  PNG 実体は `dotnet run --project ctex-to-png -- relics` で `.ctex` を変換し `tools/extracted/images/relics_png/` に生成する
- `event_images.json` — イベント ID（接頭辞なし大文字、例 `ABYSSAL_BATHS`）→ 画像のソース相対パス（`events_png/` 基準、例 `abyssal_baths.png`）。
  extractor が `tools/extracted/images/events/` ルートの `.png.import`（主画像のみ）をスキャンして生成。`Services/EventImageService.cs` で参照。
  PNG 実体は `dotnet run --project ctex-to-png -- events` で `.ctex` を変換し `tools/extracted/images/events_png/` に生成する
- `ancient_images.json` — Ancient ID（接頭辞なし大文字、例 `OROBAS`）→ 画像のソース相対パス（`ancients_png/` 基準、`_placeholder` 付き、例 `orobas_placeholder.png`）。
  extractor が `tools/extracted/images/ancients/` の `*_placeholder.png.import` をスキャンして生成（汎用フォールバックの `under_construction` は除外、画像未提供の `NEOW`/`TEZCATARA` は未収録）。`Services/AncientImageService.cs` で参照。
  PNG 実体は `dotnet run --project ctex-to-png -- ancients` で `.ctex` を変換し `tools/extracted/images/ancients_png/` に生成する
- `card_related.json` — カードがホバー表示する関連カード（DLL の `get_ExtraHoverTips`、カードのみにフィルタ）。例: `CARD.ACCURACY` → `[CARD.SHIV]`。`GetRelatedCards` / `GetCreatedByCards`（逆引き）で参照
- `keyword_dev_notes.json` — DLL 隣の `sts2.xml`（v0.107.1 から公開される .NET XML ドキュメント）の `<summary>` を抽出した開発者ノート。
  `{接頭辞}.{ID}` → 英語 summary（`<see cref>` は短縮クラス名に解決）。接頭辞は `AFFLICTION`/`ENCHANTMENT`（→ `CamelToUpperSnake` した ID、ローカライズ ID と一致）、
  `POWER`（同上）、`ENUM.{EnumType}.{Field}`（`Core.Entities.Cards.*` のフィールドのみ。`Mock*`/`Deprecated*`・ネスト型は除外）。
  個別カード/レリックは sts2.xml のドキュメント化が僅少なため対象外（`card_database` 等の IL 抽出は置き換えない）。
  `KeywordDatabaseService` が読み、`KeywordEntry.DevNoteEn`（Affliction/Enchantment エントリに付与）と
  `GetPowerNotes()`/`GetEnumNotes()`（ローカライズの無い Power/enum のメモ専用 `DevNote` エントリ）で公開。
  SiteBuilder の keywords ページが各エントリの dev ノート行と「パワー/カード系 enum」セクションを描画する

**ポーション系 JSON（card-type-extractor 生成、`Resources/{version}/`）**
- ポーションはカード・レリックと同一の IL パターンで DLL に格納される。extractor は名前空間 `MegaCrit.Sts2.Core.Models.Potions`
  のクラスを直接列挙し（`Mock*` / `Deprecated*` 除外）、各 ID は接頭辞なし大文字（例 `FIRE_POTION`、ローカライズの `potions.json` キーと一致）。
- `potion_rarities.json` — ポーション専用 enum `MegaCrit.Sts2.Core.Entities.Potions.PotionRarity`（`Common`/`Uncommon`/`Rare`/`Event`/`Token`/`Potency`、
  カード・レリックの共有 `Rarity` enum とは別物）を `get_Rarity` の `ldc.i4 + ret` から引く。enum 駆動のため Title case に正規化して出力
- `potion_stats.json` — `.ctor` のフィールド代入（`stfld` / `set_Xxx`）と `get_CanonicalVars`（`newobj XxxVar`）から得たキャノニカル変数（card/relic と同ロジック）
- `potion_characters.json` — `*PotionPool`（`SharedPotionPool` / 各キャラ / `EventPotionPool` / `TokenPotionPool`）の `GenerateAllPotions` / `GetUnlockedPotions` IL を
  generic `Add<T>()` と `newobj` 双方から走査し、プール由来の出所を `Shared` / キャラ名 / `Event` / `Token` で付与（優先順 Shared > Event > Token > 各キャラ）。
  注: 現バージョンではキャラ専用プールは空で標準ポーションは全て `Shared`。どのプールにも属さない特殊・生成系ポーションは未収録（将来プールが埋まれば自動で拾う）
- `potion_images.json` — ポーション ID（例 `FIRE_POTION`）→ 画像のソース相対パス（`potions_png/` 基準、例 `fire_potion.png`）。
  extractor が `tools/extracted/images/potions/` の `.png.import` をスキャンして生成（サブフォルダ無し）。
  PNG 実体は `dotnet run --project ctex-to-png -- potions` で `.ctex` を変換し `tools/extracted/images/potions_png/` に生成する
- `potion_database.json` — ポーションの EN/JP 表示名（`POTION.` 接頭辞）。ローカライズの `{ID}.title` から生成
- `potion_descriptions.json` — ポーションの EN/JP 説明文（生テキスト＝タグ・`{Var}` 保持）。ローカライズの `{ID}.description` から生成
- `potion_descriptions_resolved.json` — `{Var}` を `potion_stats.json` の値で実数解決（色タグ保持）。`relic_descriptions_resolved.json` と同方式

**モンスター戦闘 JSON（card-type-extractor 生成、`Resources/{version}/`）**
- `monster_combat.json` — モンスターの HP・ムーブ・インテント種別・ダメージ/ブロック値・開始パワー。
  各 `Models.Monsters` クラスの IL から抽出する（spirecodex.com/encounter 相当のデータ）。
  - HP: `get_MinInitialHp`/`get_MaxInitialHp`（`AscensionHelper.GetValueIfAscension(level, asc, base)` の ldc.i4 列で base=末尾・asc=中間、または単一 ldc=base）
  - ムーブ: `GenerateMoveStateMachine()` の `new MoveState("X_MOVE", Cb, new 〇〇Intent(dmg[,hits]), ...)`。
    `ldstr` の id から `_MOVE`/末尾数字を剥がす（ローカライズ `monsters.json` の `{ID}.moves.{KEY}` と一致）。
    インテント種別は MoveState ctor 引数の `newobj`（Single/MultiAttackIntent→ATTACK, Buff/Defend/Debuff/Stun…）
  - ダメージ/連撃: attack intent ctor 引数（getter or リテラル）。ブロック/付与パワー: ムーブ本体（async → 入れ子 `<Cb>d__N.MoveNext`）の `GainBlock` / `PowerCmd.Apply<Power>`
  - 開始パワー: `AfterAddedToRoom`（async）の `PowerCmd.Apply<Power>` 総称引数
  - 動的値（`Func`/計算式）はフィールド省略（欠損）で出力しページ側フォールバック。`MonsterCombatService` が読む
- `monster_move_patterns.json`（**手動管理**・バージョン非依存 = `Resources/` 直下）— AI 行動シーケンスの自然文 EN/JA
  （`{Var:format}` ではなく `patternEn`/`patternJa`）。`MoveStateMachine` の分岐ロジックは静的抽出が不確実なため手動アノテーション。
  エンカウンターに出る主要モンスターから段階的に追記する運用。`MonsterCombatService.GetMovePattern` が読む

**ローカライゼーション JSON（バージョン管理 = `Resources/{version}/localization/{eng,jpn}/`）**
- `relics` / `card_keywords` / `afflictions` / `enchantments` / `encounters` / `acts` / `events` / `ancients` / `potions` / `rest_site_ui` / `monsters` / `intents` / `powers` の各 `.json`。
  `tools/extracted` はゲーム更新時に内容が変わるため、card-type-extractor が抽出時に各バージョンフォルダへ**生のままコピー**して版を固定する。
  読み込みは各サービス（`KeywordDatabaseService` / `EncounterDatabaseService` / `AncientDatabaseService` / `CardDatabaseService` / `RestSiteOptionService`）が
  `ResourceResolver.ResolveVersioned(asm, "localization.{lang}.{file}.json")` で最新版を解決する。
  （`rest_site_ui.json` の `OPTION_{ID}.name` は焚き火選択肢の EN/JP 名。`RestSiteOptionService` がラン履歴の `rest_site_choices` 生 ID を日本語名へ解決）
  （`cards.json` はバージョン管理の `card_descriptions.json` / `card_database.json` に移行済み）

**主要サービス**

| ファイル | 役割 |
|---|---|
| `Services/CardDatabaseService.cs` | カード名・説明・コスト・タイプ・シナジー判定。`_regentStarSpend` 等の HashSet はクラス初期化時に一括計算される |
| `Services/EncounterDatabaseService.cs` | エンカウンター・アクト名の EN/JP ルックアップ |
| `Services/MonsterCombatService.cs` | モンスター戦闘データ（`monster_combat.json`）＋行動パターン（`monster_move_patterns.json`）。ムーブ名/インテント名/パワー名/説明は `monsters`/`intents`/`powers` ローカライズから解決 |
| `Services/DescriptionFormatter.cs` | `[gold]...[/gold]` 等の BBタグと `{Var:format}` テンプレートを除去・解決 |
| `Services/PotionOddsService.cs` | 次戦闘のポーション報酬ドロップ確率。セーブの `players[].odds.potion_reward_odds_value`（＝現在の実確率そのもの）から算出。エリート +12.5%、White Beast Statue で確定100%。根拠は `docs/potion-drop-odds.md` |
| `CharacterMechanics.cs` | キャラクター × メカニクスのフィルタ定義（`Func<string, bool>` の配列）。Toys のキャラクター概観と SiteBuilder が参照 |

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

### 配布モード（一般ユーザー向け self-contained 配布）

開発モードとは別に、ビルド済み exe を GitHub Releases で配布する（詳細設計は `docs/distribution-plan.md`）。

- **アセット解決の 2 段構え**（`StS2Shared/Services/AssetLocator.cs`）:
  開発モード＝exe から親を遡って `tools/extracted` を探す → 配布モード＝
  `%LocalAppData%\StS2Toys\assets\v{version}`（最新バージョン）を使う。どちらも無ければ未セットアップ。
- **初回セットアップ**: `SetupWizardForm` → `AssetSetup.RunSetup`（`_staging` へ抽出し成功時のみ
  `v{version}` へ原子 Move）→ `AssetExtractor.ExtractViewerAssets`（カード/レリック/エンチャント/
  ローカライズ/派生テキストの抽出＋**モンスター画像は pck 直読みで Spine レンダリング**して
  `images/monsters/{id}.png` を生成）。
- **配布 publish**: `-p:ExcludeGameText=true` でゲームテキスト埋め込みを除外し、
  `ResourceResolver.OpenText` が「埋め込み → 外部（抽出済みファイル）」の 2 段で解決する。
  タグ `v*` を push すると `.github/workflows/release.yml` が単一 exe を Releases に添付する。
- **注意点（過去に踏んだ罠）**:
  - セットアップ実行中は最終 `v{version}` が未存在。**外部解決（`OpenText`/`AssetLocator`）に頼る
    処理をセットアップ経路に置かない**（例: モンスター ID は `MonsterDatabaseService` ではなく
    pck 直列挙で得る）。
  - 認識器などのアセットディレクトリ参照は**遅延解決**にする（構築時に一度だけ解決すると、
    初回セットアップ完了後もプロセス中は未設定のままになり再起動が必要になる）。

### StS2Toys — セーブデータビューア

進行中ランのセーブを読み、デッキ・レリックを表示する。サイドのボタンで複数のサブ概観ウィンドウを開く
（いずれも `DeckOverviewForm` を再利用。カード/レリックは Bitmap に動的描画し、クリックで外部リンクを開く）。
ほかに敵情報（`EncounterOverviewForm`。遭遇済み・次のボスの先読みハイライト＋モンスター画像）、HP変動グラフ、
ポーション報酬ドロップ確率（`PotionOddsService`。サイドパネル常時表示、`docs/potion-drop-odds.md`）、
ライブ画面キャプチャ（StS2Capture.Core、`docs/StS2Toys-LiveCapture.md`）、初回セットアップウィザードを持つ。
ファイル監視の自動リロードにより、セーブ更新でこれらの表示は自動更新される。

**キャラクター概観（`btnCharacterOverview` → `EnableCharacterMode()`）**
- 1つの `DeckOverviewForm` で5キャラ分を扱う統合フォーム。上部のドロップダウンで「自動（セーブ）」＋5キャラを選択
  （`SetCurrentCharacter` がランのキャラに追従）。
- デッキを**キーワードグループ**で分類して表示（`BuildKeywordGroups` → `ComposeImageKeyword`）。グループ構成は：
  1. キャラ固有メカニクス群（`CharacterMechanics.MechanicsFor(label)` の `Func<string,bool>` フィルタ。カード・レリック両方を振り分け）
  2. **ブロック関連**（`CardDatabaseService.IsBlockGiver`。カードのみのクロス集計グループ）
  3. **プレイすると消滅する**（Power・廃棄・幽体・消滅付与エンチャント＝`IsDisposable`。旧「デッキ枚数理論値」フォームの統合先）
  4. **その他**（どのグループにも属さない残り）
- ブロック関連・消滅グループは**クロス集計**で、メカニクス群と重複するカードをそのまま重複表示する。
  ただし該当カードは `assignedCards` に登録し「その他」からは除外する（重複の二重カウント回避）。
- グループ見出しのカウントは `FormatCardCount`（`N枚中M枚(P%)`）。上部の統計バーは `BuildCharacterStats`。

### StS2SiteBuilder — 静的サイトジェネレータ

ビルドは「生成」ボタンまたは `dotnet run --project StS2SiteBuilder` で実行。

**モンスター GIF アニメーション生成（ビルド時自動）：**
- `--build` 実行時に `monster_names.json` の各モンスター **ID 単位**で
  `tools/extracted/scenes/creature_visuals/{id}.tscn`（モデル→リグ・スキン・アニメ・modulate の権威的マッピング）を
  解決し、idle アニメーションの GIF（192×192、10fps）と PNG を `{id}.gif`/`{id}.png` として
  `tools/extracted/images/monsters/` に自動生成・キャッシュする。
  - `.tscn` は `SpineSprite`（`{rig}_skel_data.tres` 参照 + `preview_skin`/`preview_animation`/`modulate`）か、
    静的 `Sprite2D`（`images/monsters/*_placeholder.png` 等）か、`visible=false`（画像なし）のいずれか。
    `CreatureVisual.cs`（`CreatureVisualParser`）が解析し、`SpineRenderer.Render` がスキン・ティント指定で描画する。
  - これにより**フォルダ名 ≠ ID のリグ**（例 `flyconid`→`flying_mushrooms`、`mysterious_knight`→`flail_knight`＋緑 tint）や、
    **1 リグをスキンで分けるモンスター**（`bowlbug_rock`=skin`rock`/`bowlbug_silk`=skin`web`、`battle_friend_v1/2/3` など）も
    正しい ID 名で画像化される（旧来のフォルダ単位描画では欠落していた）。
  - `crusher`/`rocket` など `visible=false` の内部サブモンスターは画像を生成せず timeline で `?` 表示のまま。
  - `.tscn` が無い ID は `animations/monsters/{id}/` のフォルダにフォールバックして従来通り描画する。
- キャッシュ済みは入力（`.tscn`／解決先 `.skel.import`／静的 `.ctex`）より GIF/PNG が新しければスキップ。

エンカウンター→モンスターの対応 `encounter_monsters.json`、モンスターの EN/JA 名 `monster_names.json`、
Act→イベントの対応 `event_acts.json`、Act→エンカウンターの対応 `encounter_acts.json` は、
card-type-extractor が DLL のモデルクラス
（`Models.Encounters` / `Models.Monsters` / `Models.Acts`）とローカライズから
バージョンフォルダ（`Resources/{version}/`）へ生成する（`ResourceResolver` で最新を解決）。
`encounter_acts.json` は各 Act クラスの `GenerateAllEncounters()`（ジェネリック `Add<EncounterType>()`）から
アクト別の戦闘エンカウンターを収集し、ID サフィックスで階層分け（`weak`/`normal`/`elite`/`boss`）、
ボス出現順（`get_BossDiscoveryOrder`）を `bossOrder` として持つ。`event_acts.json` と同形式の配列。
`Services/EncounterActService.cs`（`EventActService` のエンカウンター版）で読み、
SiteBuilder の `timeline.html`（spiracle.gg/monsters の Timeline タブ相当）が
アクト×階層でエンカウンター・登場モンスターを一覧表示する（`BuildTimelinePage`）。
モンスターは**モデル粒度**の ID 空間（例 `bowlbug_rock`）で、`monster_names` と `encounter_monsters` で共通。
画像/ページは ID をそのまま使うため、Spine フォルダがある ID のみ画像表示される。
Spine レンダリングには `StS2Shared.Spine`（`SpineLoader`/`SpineRenderer`/`CreatureVisual`、
SiteBuilder からは `DiskAssetSource` 経由）と
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

Ancient の DLL 由来メタデータは card-type-extractor が 2 ファイルに生成する（`Resources/{version}/`）：
- `ancient_options.json` — 各 Ancient の提示プール（レリック/カード。`{ANCIENT_ID}` → `{プール名}` → ID 配列）。
  Ancient クラス（`Models.Events` の Darv/Neow/.../Vakuu）の `get_*Option(s)` IL から `CollectGenericArgRefs` で抽出。`AncientOptionService` が読む。
- `ancient_acts.json` — 各 Ancient の登場アクト（`{ANCIENT_ID}` → `{ "act": 1|2|3 }`）。
  `ActModel.get_AllAncients` から `CollectGenericArgRefs` で抽出（Act1=Neow / Act2=Orobas・Pael・Tezcatara / Act3=Nonupeipe・Tanx・Vakuu）。
  Darv・TheArchitect はどのアクトの AllAncients にも無い特殊扱いで未収録（参照側は null=「その他」）。`AncientActService` が読む。
  SiteBuilder の ancients 一覧をアクト別グループ表示し、個別ページに「登場アクト」を出す。コスト・出現重みは DLL に無いため非対象。
  （`AncientEventModel.get_AnyCharacterDialogueBlacklist` は出現可否ではなく汎用ダイアログ選択用フラグ＝抽出対象外）
