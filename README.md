# StS2Toys

Slay the Spire 2 向けのツール集です。

---

## 配布版（一般ユーザー向け）

セーブデータビューア（StS2Toys）は、ビルド済みの実行ファイルを配布しています。
**開発環境（.NET SDK / Visual Studio）は不要**です。

### 主な機能

進行中ランのセーブを自動検出して読み込み、戦闘やイベントで内容が変わるたびに自動更新します。

- **デッキ・レリック閲覧** — 所持カード（アップグレード／エンチャント込み）とレリックを一覧表示。日本語／英語を切り替え可能。
- **デッキ概観／キャラクター概観** — カードをタイプ別・メカニクス別（ドロー／ブロック／全体攻撃／プレイで消滅 ほか）に分類して集計。キャラクター固有メカニクスにも対応。
- **HP変動グラフ** — フロアごとの HP 推移を可視化。
- **敵情報（エンカウンター概観）** — 各アクトの通常戦闘・エリート・ボスを一覧。遭遇済みや次のボスを先読みでハイライトし、モンスター画像付きで表示。
- **ポーション報酬ドロップ確率** — 次の戦闘でポーションが報酬に出る確率（通常／エリート）をセーブから算出して表示。White Beast Statue 所持時は確定表示。
- **ライブ画面キャプチャ（画面認識）** — ゲーム画面を Windows Graphics Capture で取り込み、カード選択・ショップ・エンシェントレリック選択の各画面を自動認識して候補を表示。
- **外部リンク連携** — カードやレリックをクリックで外部の攻略 wiki 等を開く（開き先はリンク設定でカスタマイズ可能）。

### 入手とセットアップ

1. [Releases](https://github.com/lichten/StS2Toys/releases) から最新の
   `StS2Toys-vX.Y.Z-win-x64.zip` をダウンロードして展開します。
2. `StS2Toys.exe` を起動します（SmartScreen が出たら「詳細情報 → 実行」）。
3. 初回のみセットアップウィザードが表示され、Steam の Slay the Spire 2 から
   画像・テキストを自動で取り込みます（数分）。以降は進行中ランのセーブを自動表示します。

- 前提: Windows 10 (1903+) / 64bit、Steam 版 Slay the Spire 2 がインストール済みであること。
- .NET ランタイムは exe に同梱されており、別途インストールは不要です。
- 配布物はゲームの著作物（画像・テキスト）を含みません。これらはお使いのゲームインストールから抽出して利用します。

以下は開発者（ソースからビルドする人）向けの手順です。

---

## 必要環境

- .NET 10 SDK
- Visual Studio 2022 以降 または Rider
- Steam 版 Slay the Spire 2 がインストール済みであること

---

## セットアップ手順

### 1. リポジトリのクローンとブランチ作成

```powershell
git clone https://github.com/lichten/StS2Toys.git
cd StS2Toys
git checkout -b feature/your-branch-name
```

### 2. tools フォルダのセットアップ

`tools/` フォルダは Git 管理外です。初回セットアップ時のみ以下の手順を実行してください。

#### 2-1. GodotPCKExplorer を入手する

[GodotPCKExplorer の GitHub リリースページ](https://github.com/DmitriySalnikov/GodotPCKExplorer/releases) から最新版の zip をダウンロードし、以下の場所に展開します。

```
tools/
└── GodotPCKExplorer/
    └── GodotPCKExplorer.Console.exe  ← ここに配置
```

#### 2-2. ゲームデータを展開する

PowerShell で以下のコマンドを実行します。

```powershell
$pck = "C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\SlayTheSpire2.pck"
$out = ".\tools\extracted"

New-Item -ItemType Directory -Force -Path $out | Out-Null
.\tools\GodotPCKExplorer\GodotPCKExplorer.Console.exe -e $pck $out
```

展開が完了すると `tools/extracted/` 以下に約 1.8 GB のゲームアセットが生成されます。

> **補足**  
> PCK バージョンは `3.4.5.1`（Godot 4.5+ 形式）です。  
> Steam のゲームアップデート後にアセットが変わった場合は、`tools/extracted/` を削除してから再実行してください。

#### 2-3. カード画像を生成する

`.ctex` 形式で圧縮されたカードアトラス画像を PNG に変換します。

```powershell
dotnet run --project ctex-to-png
```

完了すると以下が生成されます：

- `tools/extracted/images/atlases/card_atlas_0.png`（～2）— カードアトラス（アプリの画像表示に必須）
- `tools/extracted/images/card_portraits_png/{character}/*.png` — 個別カード PNG（数百枚）

> **注意**  
> このステップを省略すると、アプリ起動後にデッキ概観などでカード画像が表示されません。

### 3. ビルド・実行

tools のセットアップが完了したら、ソリューション全体をビルドします。

```powershell
dotnet build
```

各アプリケーションは以下のコマンドで起動します。

| アプリ | コマンド | 説明 |
|--------|----------|------|
| セーブデータビューア | `dotnet run --project StS2Toys` | デッキ・レリックの閲覧 |
| カードブラウザ | `dotnet run --project StS2CardBrowser` | キャラクター・メカニクスでフィルタリング |
| 静的サイトジェネレータ | `dotnet run --project StS2SiteBuilder` | ゲーム情報サイトの生成 |

---

## Disclaimer

本リポジトリは Slay the Spire 2 の非公式ファンツールです。  
Slay the Spire 2 は [Mega Crit Games](https://www.megacrit.com/) の著作物です。

**本リポジトリはゲームの著作物を含みません。**  
ゲームアセット（画像・テキスト等）は、ユーザー自身の Steam インストールから抽出して使用します（`tools/extracted/` ディレクトリ、git 管理外）。
