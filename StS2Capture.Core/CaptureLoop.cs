using StS2Capture.Capture;
using StS2Capture.Recognition;
using StS2Shared.Services;

namespace StS2Capture;

/// <summary>
/// ポーリングでフレームを取得 → 認識器でカード検出 → 「カード提示画面」ゲートを適用する。
/// 認識器・キャプチャ手段は実行中に差し替え可能。結果は <see cref="Updated"/> で通知する
/// （イベントはバックグラウンドスレッドで発火するので UI 側で Invoke すること）。
/// </summary>
public sealed class CaptureLoop : IDisposable
{
    public sealed record Result(
        string Status,
        bool IsCardScreen,
        IReadOnlyList<RecognizedCard> Cards,
        IReadOnlyList<OcrTextSpan> TextSpans,
        IReadOnlyList<Rectangle> CardBoxes,
        IReadOnlyList<Rectangle> TitleBands,
        Bitmap? Preview,
        ShopItemRecognizer.Result? Shop = null,
        ScreenRecognizer.ScreenType Screen = ScreenRecognizer.ScreenType.Unknown);

    /// <summary>ライブ表示用の縮小プレビューの最大幅（px）。</summary>
    public int PreviewMaxWidth { get; set; } = 480;

    /// <summary>相異なるカードがこの枚数以上 → カード提示画面とみなす。</summary>
    public int CardScreenThreshold { get; set; } = 2;

    public int IntervalMs { get; set; } = 800;

    /// <summary>
    /// 枠色プロファイルを選ぶキャラの手動上書き（正規化 ID、例 "DEFECT"）。
    /// null/空なら current_run.save から自動解決する。ゲーム未起動での調整に使う。
    /// </summary>
    public string? CharacterOverride { get; set; }

    /// <summary>
    /// 非 null の間、各サイクルでカード認識と同じフレームに対しショップ probe も行い、結果を
    /// <see cref="Result.Shop"/> に載せる（カード検出とショップ検出を1パスに統合）。null なら従来どおり非実行。
    /// </summary>
    public ShopItemRecognizer? ShopRecognizer { get; set; }

    /// <summary>
    /// 非 null の間、固定矩形レイアウト方式（カードを選択／ショップ）で1パス認識する。
    /// 設定時は <see cref="ShopRecognizer"/> と従来の枠色認識器（<see cref="ICardRecognizer"/>）には
    /// 依存せず、これ単体でカード・レリック・ポーションを検出して <see cref="Result"/> を埋める。
    /// </summary>
    public ScreenRecognizer? ScreenRecognizer { get; set; }

    IFrameSource _frameSource;
    ICardRecognizer _recognizer;

    // current_run.save の mtime キャッシュ（毎フレームの再読込を避ける）。
    string? _saveCharCacheId;
    DateTime _saveCharCacheMtime;

    readonly object _swap = new();
    CancellationTokenSource? _cts;
    Task? _worker;

    public event Action<Result>? Updated;

    public CaptureLoop(IFrameSource frameSource, ICardRecognizer recognizer)
    {
        _frameSource = frameSource;
        _recognizer = recognizer;
    }

    /// <summary>
    /// 枠色認識器を持たないループ。<see cref="ScreenRecognizer"/> 方式（固定矩形）専用に使う。
    /// 内部の <see cref="ICardRecognizer"/> はダミーで、ScreenRecognizer 未設定時は何も検出しない。
    /// </summary>
    public CaptureLoop(IFrameSource frameSource) : this(frameSource, NullRecognizer.Instance) { }

    /// <summary>ScreenRecognizer 方式で使う何もしない認識器（ctor の穴埋め）。</summary>
    sealed class NullRecognizer : ICardRecognizer
    {
        public static readonly NullRecognizer Instance = new();
        public string Name => "None";
        public FrameColorProfile FrameProfile { get; set; } = FrameColorProfile.SaturatedRing;
        public RecognitionResult Recognize(Bitmap frame) => RecognitionResult.Empty;
    }

    public string FrameSourceName { get { lock (_swap) return _frameSource.Name; } }
    public string RecognizerName { get { lock (_swap) return _recognizer.Name; } }

    public void SetFrameSource(IFrameSource source)
    {
        lock (_swap)
        {
            if (ReferenceEquals(_frameSource, source)) return;
            _frameSource.Dispose();
            _frameSource = source;
        }
    }

    public void SetRecognizer(ICardRecognizer recognizer)
    {
        lock (_swap) _recognizer = recognizer;
    }

    public bool IsRunning => _worker is { IsCompleted: false };

    public void Start()
    {
        if (IsRunning) return;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _worker = Task.Run(() => Loop(token), token);
    }

    public void Stop()
    {
        try { _cts?.Cancel(); } catch { }
    }

    /// <summary>1 回だけ即時にキャプチャ・認識する（手動キャプチャ用）。</summary>
    public void CaptureOnce() => RunOnce();

    /// <summary>
    /// PNG 保存用に原寸フレームを 1 枚取得する（認識はしない）。ゲーム未検出/失敗時は null。
    /// 返す Bitmap の所有権は呼び出し側。
    /// </summary>
    public Bitmap? CaptureRawFrame()
    {
        IFrameSource source;
        lock (_swap) source = _frameSource;

        var game = GameWindowLocator.Find();
        if (game is null) return null;
        try { return source.CaptureFrame(game.Value.Handle); }
        catch { return null; }
    }

    /// <summary>原寸フレームから縮小コピーを作り、カード枠（緑）・タイトル帯（赤）・
    /// ショップスロット（採用=緑/不採用=赤）を重畳する。ショップ矩形はショップ画面時のみ。</summary>
    static Bitmap MakePreview(Bitmap frame, int maxWidth,
        IReadOnlyList<Rectangle> cardBoxes, IReadOnlyList<Rectangle> titleBands,
        ShopItemRecognizer.Result? shop)
    {
        double scale = frame.Width <= maxWidth ? 1.0 : (double)maxWidth / frame.Width;
        int w = (int)Math.Round(frame.Width * scale);
        int h = Math.Max(1, (int)Math.Round(frame.Height * scale));

        var preview = new Bitmap(w, h);
        using var g = Graphics.FromImage(preview);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.DrawImage(frame, 0, 0, w, h);

        Rectangle Scaled(Rectangle b) => new(
            (int)(b.Left * scale), (int)(b.Top * scale),
            Math.Max(1, (int)(b.Width * scale)), Math.Max(1, (int)(b.Height * scale)));

        // 検出カード矩形＝緑。
        using (var green = new Pen(Color.Lime, 2f))
            foreach (var b in cardBoxes) g.DrawRectangle(green, Scaled(b));
        // タイトル帯＝赤。
        using (var red = new Pen(Color.Red, 2f))
            foreach (var b in titleBands) g.DrawRectangle(red, Scaled(b));
        // ショップスロット（ショップ画面時のみ）。採用=緑/不採用=赤。
        if (shop is { IsShop: true })
            foreach (var it in shop.Items)
                using (var pen = new Pen(it.Accepted ? Color.Lime : Color.Red, 2f))
                    g.DrawRectangle(pen, Scaled(it.Region));
        return preview;
    }

    /// <summary>
    /// 枠色プロファイル選択用に現在キャラの正規化 ID を解決する。手動上書き優先、
    /// 次に current_run.save（mtime が変わった時だけ再読込）。解決できなければ null。
    /// </summary>
    string? ResolveCharacterId()
    {
        var ov = CharacterOverride;
        if (!string.IsNullOrWhiteSpace(ov)) return ov;
        try
        {
            var path = SaveDataService.GetDefaultSavePath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            var mtime = File.GetLastWriteTimeUtc(path);
            if (mtime != _saveCharCacheMtime)
            {
                _saveCharCacheMtime = mtime;
                _saveCharCacheId = SaveDataService.TryGetCurrentCharacterId();
            }
            return _saveCharCacheId;
        }
        catch { return null; }
    }

    void Loop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            RunOnce();
            try { Task.Delay(IntervalMs, token).Wait(token); }
            catch { break; }
        }
    }

    void RunOnce()
    {
        IFrameSource source;
        ICardRecognizer recognizer;
        lock (_swap) { source = _frameSource; recognizer = _recognizer; }

        var game = GameWindowLocator.Find();
        if (game is null)
        {
            Updated?.Invoke(new Result("ゲーム未検出", false,
                Array.Empty<RecognizedCard>(), Array.Empty<OcrTextSpan>(),
                Array.Empty<Rectangle>(), Array.Empty<Rectangle>(), null));
            return;
        }

        Bitmap? frame = null;
        try
        {
            frame = source.CaptureFrame(game.Value.Handle);
            if (frame is null)
            {
                Updated?.Invoke(new Result($"キャプチャ失敗（{source.Name}）", false,
                    Array.Empty<RecognizedCard>(), Array.Empty<OcrTextSpan>(),
                    Array.Empty<Rectangle>(), Array.Empty<Rectangle>(), null));
                return;
            }

            // 現在キャラを解決（枠色プロファイル選択／固定矩形方式の候補絞り込みに使う）。
            var charId = ResolveCharacterId();

            // 固定矩形レイアウト方式が設定されていれば、それで1パス認識して終える（カード／ショップ統合）。
            var screenReco = ScreenRecognizer;
            if (screenReco is not null)
            {
                RunScreen(screenReco, game.Value.Handle, frame, source, charId);
                return;
            }

            var profile = FrameColorProfile.ForCharacter(charId);
            recognizer.FrameProfile = profile;

            var recognition = recognizer.Recognize(frame);
            int distinct = recognition.Cards.Select(c => c.CardId).Distinct().Count();
            bool isCardScreen = distinct >= CardScreenThreshold;

            // 同一フレームに対してショップ probe も実行（設定時）。カード検出と1パスに統合。
            ShopItemRecognizer.Result? shop = null;
            var shopReco = ShopRecognizer;
            if (shopReco is not null)
            {
                var client = WindowClientArea.Resolve(game.Value.Handle, frame.Width, frame.Height);
                try { shop = shopReco.Detect(frame, client); } catch { /* 失敗時はショップなし扱い */ }
            }

            var ctx = $"[キャラ:{charId ?? "自動?"} 枠:{profile.Name}]";
            string status;
            if (shop is { IsShop: true })
            {
                int acc = shop.Items.Count(i => i.Accepted);
                status = $"ショップ画面：レリック/ポーション {acc} 件（{source.Name}）{ctx}";
            }
            else
            {
                status = isCardScreen
                    ? $"カード提示画面：{distinct} 枚検出（{recognizer.Name} / {source.Name}）{ctx}"
                    : $"カード提示画面なし：検出 {distinct} 枚（{recognizer.Name} / {source.Name}）{ctx}";
            }

            // ライブ表示用に縮小プレビューを作る（カード枠/タイトル帯/ショップ枠を重畳。所有権は UI）。
            var preview = MakePreview(frame, PreviewMaxWidth,
                recognition.CardBoxes, recognition.TitleBands, shop);

            Updated?.Invoke(new Result(status, isCardScreen,
                recognition.Cards, recognition.TextSpans,
                recognition.CardBoxes, recognition.TitleBands, preview, shop));
        }
        catch (Exception ex)
        {
            Updated?.Invoke(new Result($"エラー：{ex.Message}", false,
                Array.Empty<RecognizedCard>(), Array.Empty<OcrTextSpan>(),
                Array.Empty<Rectangle>(), Array.Empty<Rectangle>(), null));
        }
        finally { frame?.Dispose(); }
    }

    /// <summary>固定矩形レイアウト方式で1フレームを認識し、結果を <see cref="Updated"/> で通知する。</summary>
    void RunScreen(ScreenRecognizer screenReco, IntPtr hwnd, Bitmap frame, IFrameSource source, string? charId)
    {
        var client = WindowClientArea.Resolve(hwnd, frame.Width, frame.Height);
        var screen = screenReco.Recognize(frame, client, charId);

        var ctx = $"[キャラ:{charId ?? "自動?"}]";
        int accessories = screen.Shop is { } s ? s.Items.Count(i => i.Accepted) : 0;
        string status = screen.Type switch
        {
            ScreenRecognizer.ScreenType.Shop =>
                $"ショップ画面：カード {screen.Cards.Count}・レリック/ポーション {accessories} 件（{source.Name}）{ctx}",
            ScreenRecognizer.ScreenType.CardSelect =>
                $"カードを選択画面：{screen.Cards.Count} 枚検出（{source.Name}）{ctx}",
            _ => $"対象画面なし（{source.Name}）{ctx}",
        };

        var preview = MakePreview(frame, PreviewMaxWidth, screen.CardBoxes, Array.Empty<Rectangle>(), screen.Shop);

        Updated?.Invoke(new Result(status, screen.Type == ScreenRecognizer.ScreenType.CardSelect,
            screen.Cards, Array.Empty<OcrTextSpan>(),
            screen.CardBoxes, Array.Empty<Rectangle>(), preview, screen.Shop, screen.Type));
    }

    public void Dispose()
    {
        Stop();
        try { _worker?.Wait(1000); } catch { }
        lock (_swap) _frameSource.Dispose();
        _cts?.Dispose();
    }
}
