using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace StS2Capture.Recognition;

/// <summary>
/// フレーム全体を Windows 標準 OCR にかけ、認識した各行を <see cref="CardNameIndex"/> に
/// 照合してカードを特定する（主実装）。画面種別を問わず「出ているカード名」を拾えるため、
/// 報酬／ショップ／イベントを横断カバーできる。
/// </summary>
public sealed class OcrCardRecognizer : ICardRecognizer
{
    public string Name => "OCR";

    readonly CardNameIndex _index;
    readonly OcrEngine? _engine;
    readonly CardRegionDetector _detector = new();

    public OcrCardRecognizer(CardNameIndex index)
    {
        _index = index;
        _engine = CreateEngine();
    }

    /// <summary>カード矩形検出器（縁色しきい値の調整用に公開）。</summary>
    public CardRegionDetector Detector => _detector;

    /// <summary>枠色プロファイル（キャラ別）。矩形検出器へ転送する。</summary>
    public FrameColorProfile FrameProfile
    {
        get => _detector.ActiveProfile;
        set => _detector.ActiveProfile = value;
    }

    /// <summary>OCR エンジンが利用可能か（言語パック未導入だと null）。</summary>
    public bool IsAvailable => _engine is not null;

    /// <summary>タイトル帯の拡大倍率。</summary>
    public int TitleScale { get; set; } = 3;

    /// <summary>
    /// 非 null の間、タイトル帯の二値化クロップをこのフォルダに PNG 保存する（実機調整用）。
    /// UI が手動キャプチャ時に一時的に設定する想定。
    /// </summary>
    public string? SaveTitleCropsDir { get; set; }

    static int _cropSeq;

    static OcrEngine? CreateEngine()
    {
        // ユーザのプロファイル言語（多くは日本語）でエンジンを作る。
        var engine = OcrEngine.TryCreateFromUserProfileLanguages();
        if (engine is not null) return engine;
        // フォールバックで英語を試す。
        try { return OcrEngine.TryCreateFromLanguage(new Language("en")); }
        catch { return null; }
    }

    public RecognitionResult Recognize(Bitmap frame)
    {
        if (_engine is null) return RecognitionResult.Empty;

        // 1) 全画面 OCR（説明文など標準フォントは読める）。
        var result = RunOcr(frame);
        if (result is null) return RecognitionResult.Empty;

        var found = new Dictionary<string, RecognizedCard>(StringComparer.Ordinal);
        var spans = new List<OcrTextSpan>(result.Lines.Count);

        foreach (var line in result.Lines)
        {
            var region = BoundingBox(line);
            var match = _index.FindBest(line.Text);
            spans.Add(new OcrTextSpan(line.Text, region,
                match?.CardId, match?.Distance, match?.Confidence, "frame"));
            if (match is not null)
                Merge(found, match.Value.CardId, line.Text, region, match.Value.Confidence, "OCR");
        }

        // 2) カード矩形を縁色で検出し、各カードのタイトル小領域を局所 OCR する。
        var cardBoxes = _detector.Detect(frame);
        TrySaveMask();

        var titleBands = new List<Rectangle>(cardBoxes.Count);
        foreach (var card in cardBoxes)
        {
            var band = Rectangle.Intersect(
                CardRegionDetector.TitleRegionOf(card),
                new Rectangle(0, 0, frame.Width, frame.Height));
            if (band.Width < 8 || band.Height < 6) continue;
            titleBands.Add(band);

            using var crop = ImageOps.CropUpscaleBinarize(frame, band, Math.Max(1, TitleScale));
            TrySaveCrop(crop);

            var tr = RunOcr(crop);
            if (tr is null) continue;

            // クロップ内の各行を照合し、最良マッチを採用。
            CardNameIndex.Match? best = null;
            string bestText = "";
            foreach (var line in tr.Lines)
            {
                var m = _index.FindBest(line.Text);
                if (m is null) continue;
                if (best is null || m.Value.Confidence > best.Value.Confidence)
                {
                    best = m; bestText = line.Text;
                }
            }

            var joined = string.Join(" ", tr.Lines.Select(l => l.Text));
            spans.Add(new OcrTextSpan(
                string.IsNullOrWhiteSpace(joined) ? "(タイトル OCR: 空)" : joined,
                band, best?.CardId, best?.Distance, best?.Confidence, "title"));

            if (best is not null)
                Merge(found, best.Value.CardId, bestText, band, best.Value.Confidence, "OCR-title");
        }

        var cards = found.Values.OrderBy(r => r.Region.Left).ToList();
        return new RecognitionResult(cards, spans, cardBoxes, titleBands);
    }

    /// <summary>同一 CardId はタイトル由来＞確信度の優先で採用する。</summary>
    static void Merge(Dictionary<string, RecognizedCard> found, string cardId,
        string text, Rectangle region, double confidence, string recognizer)
    {
        var rc = new RecognizedCard(cardId, text, region, confidence, recognizer);
        if (!found.TryGetValue(cardId, out var prev))
        {
            found[cardId] = rc;
            return;
        }
        bool prevIsTitle = prev.Recognizer == "OCR-title";
        bool newIsTitle = recognizer == "OCR-title";
        if (newIsTitle && !prevIsTitle) found[cardId] = rc;       // タイトル由来を優先
        else if (newIsTitle == prevIsTitle && confidence > prev.Confidence) found[cardId] = rc;
    }

    OcrResult? RunOcr(Bitmap bmp)
    {
        SoftwareBitmap? sb = null;
        try { sb = ToSoftwareBitmap(bmp); }
        catch { return null; }
        try { return _engine!.RecognizeAsync(sb).AsTask().GetAwaiter().GetResult(); }
        catch { return null; }
        finally { sb?.Dispose(); }
    }

    void TrySaveCrop(Bitmap crop)
    {
        var dir = SaveTitleCropsDir;
        if (dir is null) return;
        try
        {
            Directory.CreateDirectory(dir);
            int n = System.Threading.Interlocked.Increment(ref _cropSeq);
            crop.Save(Path.Combine(dir, $"title_{n:D4}.png"), System.Drawing.Imaging.ImageFormat.Png);
        }
        catch { /* 保存失敗は無視 */ }
    }

    /// <summary>縁色マスクを PNG 保存する（カード検出の調整用）。SaveTitleCropsDir が非 null のとき。</summary>
    void TrySaveMask()
    {
        var dir = SaveTitleCropsDir;
        if (dir is null || _detector.LastMask is null) return;
        try
        {
            Directory.CreateDirectory(dir);
            int n = System.Threading.Interlocked.Increment(ref _cropSeq);
            using var bmp = ImageOps.MaskToBitmap(_detector.LastMask, _detector.LastMaskW, _detector.LastMaskH);
            bmp.Save(Path.Combine(dir, $"mask_{n:D4}.png"), System.Drawing.Imaging.ImageFormat.Png);
        }
        catch { /* 保存失敗は無視 */ }
    }

    static Rectangle BoundingBox(OcrLine line)
    {
        double l = double.MaxValue, t = double.MaxValue, r = 0, b = 0;
        foreach (var w in line.Words)
        {
            var br = w.BoundingRect;
            l = Math.Min(l, br.X);
            t = Math.Min(t, br.Y);
            r = Math.Max(r, br.X + br.Width);
            b = Math.Max(b, br.Y + br.Height);
        }
        if (l == double.MaxValue) return Rectangle.Empty;
        return Rectangle.FromLTRB((int)l, (int)t, (int)r, (int)b);
    }

    /// <summary>System.Drawing.Bitmap → SoftwareBitmap(Bgra8)。</summary>
    static SoftwareBitmap ToSoftwareBitmap(Bitmap bmp)
    {
        int w = bmp.Width, h = bmp.Height;
        var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);
        var bytes = new byte[w * 4 * h];
        try
        {
            for (int y = 0; y < h; y++)
                Marshal.Copy(data.Scan0 + y * data.Stride, bytes, y * w * 4, w * 4);
        }
        finally { bmp.UnlockBits(data); }

        var sb = new SoftwareBitmap(BitmapPixelFormat.Bgra8, w, h, BitmapAlphaMode.Premultiplied);
        using var dw = new DataWriter();
        dw.WriteBytes(bytes);
        sb.CopyFromBuffer(dw.DetachBuffer());
        return sb;
    }
}
