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
        Bitmap? Preview);

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

    /// <summary>原寸フレームから縮小コピーを作り、カード枠（緑）とタイトル帯（赤）を重畳する。</summary>
    static Bitmap MakePreview(Bitmap frame, int maxWidth,
        IReadOnlyList<Rectangle> cardBoxes, IReadOnlyList<Rectangle> titleBands)
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

            // 現在キャラを解決し、対応する枠色プロファイルを認識器（=矩形検出器）へ設定する。
            var charId = ResolveCharacterId();
            var profile = FrameColorProfile.ForCharacter(charId);
            recognizer.FrameProfile = profile;

            var recognition = recognizer.Recognize(frame);
            int distinct = recognition.Cards.Select(c => c.CardId).Distinct().Count();
            bool isCardScreen = distinct >= CardScreenThreshold;

            var ctx = $"[キャラ:{charId ?? "自動?"} 枠:{profile.Name}]";
            var status = isCardScreen
                ? $"カード提示画面：{distinct} 枚検出（{recognizer.Name} / {source.Name}）{ctx}"
                : $"カード提示画面なし：検出 {distinct} 枚（{recognizer.Name} / {source.Name}）{ctx}";

            // ライブ表示用に縮小プレビューを作る（カード枠＝緑・タイトル帯＝赤を重畳。所有権は UI）。
            var preview = MakePreview(frame, PreviewMaxWidth, recognition.CardBoxes, recognition.TitleBands);

            Updated?.Invoke(new Result(status, isCardScreen,
                recognition.Cards, recognition.TextSpans,
                recognition.CardBoxes, recognition.TitleBands, preview));
        }
        catch (Exception ex)
        {
            Updated?.Invoke(new Result($"エラー：{ex.Message}", false,
                Array.Empty<RecognizedCard>(), Array.Empty<OcrTextSpan>(),
                Array.Empty<Rectangle>(), Array.Empty<Rectangle>(), null));
        }
        finally { frame?.Dispose(); }
    }

    public void Dispose()
    {
        Stop();
        try { _worker?.Wait(1000); } catch { }
        lock (_swap) _frameSource.Dispose();
        _cts?.Dispose();
    }
}
