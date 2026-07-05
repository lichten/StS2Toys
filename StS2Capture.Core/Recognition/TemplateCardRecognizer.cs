using StS2Shared.Services;

namespace StS2Capture.Recognition;

/// <summary>
/// カード矩形の絵窓を card_portraits_png の portrait と HSV カラーヒストグラムで照合して識別する。
/// 事前検証でグレースケール構造照合は失敗、HSV カラーヒストグラム＋カイ二乗距離は明確な差で正解1位だった。
/// </summary>
public sealed class TemplateCardRecognizer : ICardRecognizer
{
    public string Name => "Template";

    // portrait の下端トリム（在ゲーム絵窓の種別装飾を除外するため対応させる。ArtRegionOf と同値）。
    const double PortraitBottomTrim = 0.15;

    // 採否しきい値（調整可能）。best が小さく、2位とのマージンがある時のみ採用。
    public double MaxDistance { get; set; } = 0.85;
    public double MinMargin { get; set; } = 0.04;

    readonly CardRegionDetector _detector = new();
    readonly string? _portraitsDir;
    Dictionary<string, float[]>? _db; // CardId → 正規化 HSV ヒストグラム（遅延構築）

    public TemplateCardRecognizer()
    {
        _portraitsDir = ResolvePortraitsDir();
    }

    public bool IsAvailable => _portraitsDir is not null;

    /// <summary>枠色プロファイル（キャラ別）。矩形検出器へ転送する。</summary>
    public FrameColorProfile FrameProfile
    {
        get => _detector.ActiveProfile;
        set => _detector.ActiveProfile = value;
    }

    public readonly record struct Match(string CardId, double Distance, double Confidence);

    public RecognitionResult Recognize(Bitmap frame)
    {
        var db = EnsureDb();
        var cardBoxes = _detector.Detect(frame);
        if (db.Count == 0 || cardBoxes.Count == 0)
            return new RecognitionResult(
                Array.Empty<RecognizedCard>(), Array.Empty<OcrTextSpan>(),
                cardBoxes, Array.Empty<Rectangle>());

        var cards = new List<RecognizedCard>();
        var artRegions = new List<Rectangle>(cardBoxes.Count);
        foreach (var card in cardBoxes)
        {
            var art = Rectangle.Intersect(
                CardRegionDetector.ArtRegionOf(card),
                new Rectangle(0, 0, frame.Width, frame.Height));
            if (art.Width < 8 || art.Height < 8) continue;
            artRegions.Add(art);

            var m = Identify(frame, art, db);
            if (m is not null)
                cards.Add(new RecognizedCard(m.Value.CardId, "", art, m.Value.Confidence, "Template"));
        }

        return new RecognitionResult(cards, Array.Empty<OcrTextSpan>(), cardBoxes, artRegions);
    }

    /// <summary>絵窓のヒストグラムを DB 全件と照合し、最近傍を返す（しきい値・マージンで採否）。</summary>
    Match? Identify(Bitmap frame, Rectangle art, Dictionary<string, float[]> db)
    {
        var q = HsvHistogram.Compute(frame, art);
        string? bestId = null; double best = double.MaxValue, second = double.MaxValue;
        foreach (var (id, h) in db)
        {
            double d = HsvHistogram.ChiSquare(q, h);
            if (d < best) { second = best; best = d; bestId = id; }
            else if (d < second) second = d;
        }
        if (bestId is null || best > MaxDistance || (second - best) < MinMargin) return null;

        // 距離とマージンから簡易 confidence。
        double conf = Math.Clamp((1.0 - best) * 0.5 + Math.Min(1.0, (second - best) / 0.3) * 0.5, 0, 1);
        return new Match(bestId, best, conf);
    }

    Dictionary<string, float[]> EnsureDb()
    {
        if (_db is not null) return _db;
        _db = new();
        if (_portraitsDir is null) return _db;

        foreach (var id in CardDatabaseService.GetAllCardIds())
        {
            var path = CardImageService.GetSourcePath(_portraitsDir, id);
            if (path is null || !File.Exists(path)) continue;
            try
            {
                using var bmp = new Bitmap(path);
                // 在ゲーム絵窓は下 15%（種別装飾）を除外するので、portrait も上 85% に揃える。
                int ph = Math.Max(1, (int)(bmp.Height * (1.0 - PortraitBottomTrim)));
                _db[id] = HsvHistogram.Compute(bmp, new Rectangle(0, 0, bmp.Width, ph));
            }
            catch { /* 壊れた画像はスキップ */ }
        }
        return _db;
    }

    /// <summary>card_portraits_png ディレクトリを <see cref="AssetLocator"/> 経由で解決する。</summary>
    static string? ResolvePortraitsDir()
    {
        var candidate = AssetLocator.ImagesDir(CardImageService.PortraitsDirName);
        return candidate != null && Directory.Exists(candidate) ? candidate : null;
    }
}
