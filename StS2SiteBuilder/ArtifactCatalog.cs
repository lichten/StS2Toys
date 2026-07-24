// 成果物グループの静的メタデータ（生成元・コマンド・出力先・参照元）。
// スキャン結果（ArtifactInventory）と Key で突き合わせて表示する。
// 出典は CLAUDE.md のデータフロー記述 — 生成コマンドや参照元が変わったらここも更新する。

record ArtifactGroup(
    string Key,          // スキャン結果との突き合わせキー
    string Title,
    string Producer,     // 生成元プロジェクト（manual = 手動管理）
    string Command,      // 再生成コマンド
    string Output,       // 出力先（リポジトリ相対）
    string[] Consumers,  // 参照元（ソリューション内・外部）
    string Notes);

static class ArtifactCatalog
{
    public static readonly ArtifactGroup[] Groups =
    [
        new(
            Key: "resources",
            Title: "メタデータ JSON（バージョン別）",
            Producer: "card-type-extractor",
            Command: "dotnet run --project card-type-extractor",
            Output: @"StS2Shared\Resources\{version}\",
            Consumers:
            [
                "StS2Shared 埋め込みリソース（ResourceResolver が最新版を解決）→ SpireScope / StS2Capture",
                "site3 の scripts/import.php（取り込み時に参照）",
            ],
            Notes: "ゲーム DLL の IL 解析＋ローカライズ抽出。localization/{eng,jpn} は生コピーで版固定。"),
        new(
            Key: "manual",
            Title: "手動管理 JSON（バージョン非依存）",
            Producer: "manual",
            Command: "（手動編集）",
            Output: @"StS2Shared\Resources\",
            Consumers: ["MonsterCombatService（行動パターンの自然文 EN/JA）"],
            Notes: "monster_move_patterns.json。MoveStateMachine の分岐は静的抽出が不確実なため手動アノテーション。"),
        new(
            Key: "card_portraits_png",
            Title: "カードポートレート PNG",
            Producer: "ctex-to-png",
            Command: "dotnet run --project ctex-to-png",
            Output: @"tools\extracted\images\card_portraits_png\",
            Consumers:
            [
                "card-type-extractor（card_images.json 生成時の実ファイルスキャン元）",
                "SpireScope のカード描画（CardImageService 経由）",
            ],
            Notes: "クリーン展開直後は未生成 → extractor → ctex-to-png（無引数）→ extractor 再実行の順で回す。"),
        new(
            Key: "relics_png",
            Title: "レリック PNG",
            Producer: "ctex-to-png",
            Command: "dotnet run --project ctex-to-png -- relics",
            Output: @"tools\extracted\images\relics_png\",
            Consumers: ["RelicImageService（relic_images.json 経由）"],
            Notes: ""),
        new(
            Key: "events_png",
            Title: "イベント PNG",
            Producer: "ctex-to-png",
            Command: "dotnet run --project ctex-to-png -- events",
            Output: @"tools\extracted\images\events_png\",
            Consumers: ["EventImageService（event_images.json 経由）"],
            Notes: ""),
        new(
            Key: "ancients_png",
            Title: "エンシェント PNG",
            Producer: "ctex-to-png",
            Command: "dotnet run --project ctex-to-png -- ancients",
            Output: @"tools\extracted\images\ancients_png\",
            Consumers: ["AncientImageService（ancient_images.json 経由）"],
            Notes: ""),
        new(
            Key: "potions_png",
            Title: "ポーション PNG",
            Producer: "ctex-to-png",
            Command: "dotnet run --project ctex-to-png -- potions",
            Output: @"tools\extracted\images\potions_png\",
            Consumers: ["PotionImageService（potion_images.json 経由）"],
            Notes: "v0.109.0 で本体画像が large/ サブフォルダへ移動。"),
        new(
            Key: "monsters",
            Title: "モンスター画像（PNG / GIF）",
            Producer: "ctex-to-png",
            Command: "dotnet run --project ctex-to-png -- monsters",
            Output: @"tools\extracted\images\monsters\",
            Consumers:
            [
                "site3（slaythespire2.lichtenlab.com）が read-only マウントで直接参照（docker-compose の images/monsters:/var/www/html/img/monsters:ro）",
                "site3 の scripts/import.php が取り込み時に欠落を検出して警告",
                "SpireScope の敵情報概観（EncounterOverviewForm）",
            ],
            Notes: "クリーン展開で必ず消える — 展開後は必ず再実行すること。crusher/rocket 等 visible=false の ID は正当に画像なし。"),
        new(
            Key: "atlases",
            Title: "カードアトラス PNG",
            Producer: "ctex-to-png",
            Command: "dotnet run --project ctex-to-png",
            Output: @"tools\extracted\images\atlases\",
            Consumers: ["（デバッグ・参照用のみ）"],
            Notes: ""),
    ];
}
