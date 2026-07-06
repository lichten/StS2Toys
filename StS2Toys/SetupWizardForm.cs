using StS2Shared.Assets;
using StS2Shared.Services;

namespace StS2Toys;

/// <summary>
/// 初回セットアップ／再同期ウィザード。ユーザーの Slay the Spire 2 インストールから画像アセットを
/// 抽出して配布ディレクトリ（<c>%LocalAppData%\StS2Toys\assets\v{version}</c>）へ配置する。
/// ゲームデータの再配布は行わず、抽出はユーザーの手元で完結する。抽出処理の実体は
/// <see cref="AssetSetup.RunSetup"/>（UI 非依存・トランザクショナル）。
/// </summary>
public partial class SetupWizardForm : Form
{
    public enum SetupOutcome { Skipped, Completed }

    /// <summary>ユーザーの選択結果。既定はスキップ。</summary>
    public SetupOutcome Outcome { get; private set; } = SetupOutcome.Skipped;

    /// <summary>抽出完了時のバージョン名（例 "v0.108.0"）。完了時のみ非 null。</summary>
    public string? InstalledVersion { get; private set; }

    readonly string? _updateFromVersion;
    Sts2Install? _install;
    CancellationTokenSource? _cts;

    /// <param name="updateFromVersion">再同期（ゲーム更新）時に、現在導入済みのバージョンを渡すと文言を切り替える。初回は null。</param>
    public SetupWizardForm(string? updateFromVersion = null)
    {
        _updateFromVersion = updateFromVersion;
        InitializeComponent();

        _lblTitle.Text = _updateFromVersion is null
            ? "画像アセットのセットアップ"
            : "ゲームの更新を検出しました";
        _lblDesc.Text =
            "Slay the Spire 2 のインストールからカード・レリック等の画像を取り込みます。\n" +
            "ゲームデータの再配布は行わず、抽出はこの PC 内で完結します。\n" +
            "画像なしでもテキスト表示は動作するため、この手順は後から実行することもできます。";

        _btnBrowse.Click += (_, _) => Browse();
        _btnStart.Click += async (_, _) => await RunAsync();
        _btnCancel.Click += (_, _) => _cts?.Cancel();
        _btnSkip.Click += (_, _) => { Outcome = SetupOutcome.Skipped; DialogResult = DialogResult.Cancel; Close(); };

        // 進捗ラベルは初期空だと 1 行分の高さを持たず、抽出開始で文言が入った瞬間に
        // 高さが増えて下部が見切れうる。常に 1 行分を確保するためプレースホルダを入れる。
        _lblProgress.Text = " ";

        Load += (_, _) => Detect();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e); // 先に Detect() が走り _lblStatus 文言（1〜2行）が確定する
        // ラップ確定後（フォント DPI スケール適用済み）の内容高さを実測し、足りなければ
        // フォームを広げる。幅を渡して測ることで複数行ラベルが正しい行数で高さ計算される。
        // 固定高だと日本語＋高 DPI で下部（進捗・ボタン列）が見切れるため。
        int needed = _root.GetPreferredSize(new Size(ClientSize.Width, 0)).Height;
        if (needed > ClientSize.Height)
            ClientSize = new Size(ClientSize.Width, needed);
    }

    void Detect()
    {
        _install = SteamLocator.Locate();
        UpdateStatus();
    }

    void UpdateStatus()
    {
        if (_install is null)
        {
            _lblStatus.Text = "Slay the Spire 2 を自動検出できませんでした。\n「参照...」でゲームフォルダか SlayTheSpire2.pck を指定してください。";
            _btnStart.Enabled = false;
        }
        else
        {
            _lblStatus.Text = $"検出: {_install.GameDir}\nバージョン: {_install.Version ?? "(不明)"}";
            _btnStart.Enabled = true;
        }
    }

    void Browse()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "Slay the Spire 2 のインストールフォルダを選択してください",
            UseDescriptionForTitle = true,
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        var found = SteamLocator.FromPath(dlg.SelectedPath);
        if (found is null)
            MessageBox.Show(this,
                "選択したフォルダに SlayTheSpire2.pck が見つかりませんでした。",
                "セットアップ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        else
            _install = found;
        UpdateStatus();
    }

    async Task RunAsync()
    {
        if (_install is null) return;

        _cts = new CancellationTokenSource();
        SetRunning(true);
        var progress = new Progress<ExtractProgress>(OnProgress);

        try
        {
            var install = _install;
            var token = _cts.Token;
            var final = await Task.Run(() => AssetSetup.RunSetup(install, progress, token));
            InstalledVersion = Path.GetFileName(final);
            Outcome = SetupOutcome.Completed;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (OperationCanceledException)
        {
            _lblProgress.Text = "キャンセルしました。";
            SetRunning(false);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"抽出に失敗しました:\n{ex.Message}",
                "セットアップ", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _lblProgress.Text = "失敗しました。";
            SetRunning(false);
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
        }
    }

    void OnProgress(ExtractProgress p)
    {
        _progress.Maximum = Math.Max(1, p.Total);
        _progress.Value = Math.Min(p.Done, _progress.Maximum);
        _lblProgress.Text = $"{GroupLabel(p.Group)}  {p.Done}/{p.Total}";
    }

    void SetRunning(bool running)
    {
        _btnStart.Enabled = !running && _install is not null;
        _btnBrowse.Enabled = !running;
        _btnSkip.Enabled = !running;
        _btnCancel.Enabled = running;
        if (running) _progress.Value = 0;
    }

    static string GroupLabel(string group) => group switch
    {
        "card_portraits" => "カード画像",
        "relics" => "レリック画像",
        "card_atlas_sprites" => "カードアトラス定義",
        "card_atlas_png" => "カードアトラス",
        "relic_atlas" => "レリックアトラス",
        "enchantments" => "エンチャント",
        "localization" => "テキスト",
        "game_text" => "ゲームテキスト生成",
        _ => group,
    };
}
