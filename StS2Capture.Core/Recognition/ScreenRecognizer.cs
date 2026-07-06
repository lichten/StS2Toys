using StS2Shared.Services;

namespace StS2Capture.Recognition;

/// <summary>
/// 固定矩形レイアウト方式の画面認識器。提示画面は実質「カードを選択」と「ショップ」の2種類しか
/// 無いので、画面ごとにイラスト矩形を固定座標で probe して中身を識別する（枠色による矩形探索は廃止）。
/// レリック／ポーションは <see cref="ShopItemRecognizer"/> を合成して委譲し、カードは本クラスが
/// キャラ別 portrait DB との HSV ヒストグラム照合で識別する。各カードスロットの外枠色から
/// キャラを推定し（<see cref="FrameColorClassifier"/>）、照合候補プールを絞り込む。
/// 画面種別は両レイアウトの固定スロットを試して確信一致数で判定する（probe-as-detector）。
/// 座標はクライアント領域基準の相対値（解像度・タイトルバー有無に非依存）。
/// </summary>
public sealed class ScreenRecognizer
{
    public enum ScreenType { Unknown, CardSelect, Shop, AncientSelect }

    /// <summary>カード全体矩形をクライアント相対で表す（中心 + 大きさ）。アート窓・枠帯はここから導出する。</summary>
    public readonly record struct CardSlot(double CxFrac, double CyFrac, double WFrac, double HFrac);

    public sealed record ScreenResult(
        ScreenType Type,
        IReadOnlyList<RecognizedCard> Cards,
        ShopItemRecognizer.Result? Shop,
        IReadOnlyList<Rectangle> CardBoxes);

    /// <summary>採否しきい値（best がこの距離以内なら採用）。<see cref="TemplateCardRecognizer"/> 同値を初期値に。</summary>
    public double MaxDistance { get; set; } = 0.85;

    /// <summary>best と2位の最小マージン（タイは不採用）。</summary>
    public double MinMargin { get; set; } = 0.04;

    /// <summary>カード選択画面とみなす最小の確信一致カード数。</summary>
    public int MinCardsForSelect { get; set; } = 2;

    /// <summary>カード選択判定に必要な1枚あたり最低確信度（誤検出防止。実機で較正）。</summary>
    public double MinSelectConfidence { get; set; } = 0.30;

    /// <summary>portrait 下端トリム（種別装飾を除外。<see cref="TemplateCardRecognizer"/> と同値）。</summary>
    const double PortraitBottomTrim = 0.15;

    /// <summary>
    /// 「カードを選択」画面の3カードの全体矩形（クライアント相対）。
    /// 初期値は提供スクショからの目視概算。実機で <see cref="SaveCropsDir"/> を使い較正すること。
    /// </summary>
    // Silent カード選択画面（1287×765, client 原点(0,45)・1287×720）を枠色二値化して実測した
    // 緑フレーム外接矩形（CardRegionDetector + SilentGreen）。ArtWindow=ArtRegionOf がこの矩形に合う。
    public List<CardSlot> CardSelectSlots { get; set; } = new()
    {
        new(0.320, 0.597, 0.127, 0.263),
        new(0.500, 0.597, 0.127, 0.263),
        new(0.681, 0.597, 0.127, 0.263),
    };

    /// <summary>
    /// ショップ画面の売り物カードの全体矩形（上段5＋下段2、クライアント相対）。
    /// 初期値は目視概算。実機較正必須。
    /// </summary>
    // Regent ショップ画面（1287×765, client 原点(0,45)・1287×720）を枠色二値化して実測した
    // フレーム外接矩形（CardRegionDetector + RegentOrange/ColorlessGray）。上段5（橙）＋下段2（灰）。
    public List<CardSlot> ShopCardSlots { get; set; } = new()
    {
        new(0.230, 0.376, 0.103, 0.214), new(0.366, 0.374, 0.104, 0.218),
        new(0.501, 0.376, 0.104, 0.214), new(0.635, 0.376, 0.104, 0.214),
        new(0.772, 0.374, 0.104, 0.214),
        new(0.264, 0.721, 0.104, 0.214), new(0.406, 0.721, 0.104, 0.214),
    };

    /// <summary>非 null の間、各カードスロットの切り出しを PNG 保存する（実機較正用）。</summary>
    public string? SaveCropsDir { get; set; }
    static int _cropSeq;

    readonly ShopItemRecognizer _shop;
    readonly AncientRelicRecognizer _ancient = new();
    // 配布モードでは初回セットアップ完了前（Form1 構築時）に本認識器が作られ得るため、
    // ディレクトリは遅延解決する（アセット導入後に自動で拾い、再起動不要にする）。
    string? _portraitsDir;
    string? PortraitsDir => _portraitsDir ??= ResolvePortraitsDir();

    // キャラ正規化キー（大文字、"" = ニュートラル）→ (CardId, 正規化 HSV ヒストグラム)。遅延構築。
    Dictionary<string, List<(string Id, float[] Hist)>>? _cardDb;
    List<(string Id, float[] Hist)>? _cardDbAll;

    public ScreenRecognizer(ShopItemRecognizer shop) => _shop = shop;

    public bool IsAvailable => PortraitsDir is not null;

    /// <summary>
    /// カード／レリック／ポーション／エンシェントの照合 DB を事前構築する（キャプチャは行わない）。
    /// 初回キャプチャで同期構築される重い遅延ビルドを、起動直後の背景スレッドで前倒しして体感を改善する。
    /// アセット未解決時は各ビルダが空で即返るため無害（次回以降に遅延構築される）。
    /// </summary>
    public void Warmup()
    {
        EnsureCardDb();
        _shop.Warmup();
        _ancient.Warmup();
    }

    readonly record struct Match(string CardId, double Distance, double Confidence);

    public ScreenResult Recognize(Bitmap frame, Rectangle client, string? currentCharacterId)
    {
        // 1) レリック／ポーションスロットを probe（ショップ判定の主信号）。
        ShopItemRecognizer.Result? shop = null;
        try { shop = _shop.Detect(frame, client); } catch { /* 失敗時はショップなし扱い */ }

        if (shop is { IsShop: true })
        {
            var shopCards = ProbeCards(frame, client, ShopCardSlots, currentCharacterId);
            return new ScreenResult(ScreenType.Shop, shopCards, shop, CardRects(client, ShopCardSlots));
        }

        // 2) エンシェントレリック選択画面（固定バンドのレリック名を OCR 照合）。
        ShopItemRecognizer.Result? ancient = null;
        try { ancient = _ancient.Detect(frame, client); } catch { /* 失敗時は非該当扱い */ }
        if (ancient is { IsShop: true })
            return new ScreenResult(ScreenType.AncientSelect,
                Array.Empty<RecognizedCard>(), ancient, Array.Empty<Rectangle>());

        // 3) いずれでもなければカード選択スロットを probe。
        var cards = ProbeCards(frame, client, CardSelectSlots, currentCharacterId);
        // 相異なるカードが必要数あり、かつ確信カードも必要数ある場合のみ「カードを選択」とみなす。
        // （同一カードばかり＝誤検出、全て低確信＝枠色類似イラストの誤一致を弾く）
        int distinctCards = cards.Select(c => c.CardId).Distinct().Count();
        int confidentCards = cards.Count(c => c.Confidence >= MinSelectConfidence);
        if (distinctCards >= MinCardsForSelect && confidentCards >= MinCardsForSelect)
            return new ScreenResult(ScreenType.CardSelect, cards, null, CardRects(client, CardSelectSlots));

        return new ScreenResult(ScreenType.Unknown, Array.Empty<RecognizedCard>(), null, Array.Empty<Rectangle>());
    }

    /// <summary>各カードスロットのアート窓を識別し、採用できたカードを返す（枠色でプール絞り込み）。</summary>
    List<RecognizedCard> ProbeCards(Bitmap frame, Rectangle client,
        List<CardSlot> slots, string? currentCharacterId)
    {
        var db = EnsureCardDb();
        var result = new List<RecognizedCard>(slots.Count);
        if (db.Count == 0) return result;
        var frameRect = new Rectangle(0, 0, frame.Width, frame.Height);

        foreach (var slot in slots)
        {
            var cardRect = Rectangle.Intersect(ToPixels(client, slot), frameRect);
            if (cardRect.Width < 16 || cardRect.Height < 16) continue;
            TrySaveCrop(frame, cardRect);

            var art = Rectangle.Intersect(ArtWindow(cardRect), frameRect);
            if (art.Width < 8 || art.Height < 8) continue;

            // 枠色 → キャラ推定 → 照合プールを絞る（推定不能なら現在キャラ／全件にフォールバック）。
            var ringChar = FrameColorClassifier.Classify(frame, cardRect);
            var pool = SelectPool(ringChar, currentCharacterId);

            var m = Identify(frame, art, pool);
            if (m is not null)
                result.Add(new RecognizedCard(m.Value.CardId, "", art, m.Value.Confidence, "FixedRect"));
        }
        return result;
    }

    /// <summary>
    /// 照合候補プールを選ぶ。枠色推定キャラ・現在キャラ・ニュートラル（colorless 等）を合算する。
    /// どちらの手掛かりも無い時のみ全件にフォールバックする。
    /// </summary>
    List<(string Id, float[] Hist)> SelectPool(string? ringChar, string? currentCharacterId)
    {
        var db = _cardDb!;
        bool hasSignal = ringChar is not null || !string.IsNullOrEmpty(currentCharacterId);
        if (!hasSignal) return _cardDbAll!;

        var keys = new HashSet<string>(StringComparer.Ordinal) { "" }; // ニュートラルは常に含める
        if (ringChar is not null) keys.Add(ringChar);
        if (!string.IsNullOrEmpty(currentCharacterId)) keys.Add(currentCharacterId!.ToUpperInvariant());

        var pool = new List<(string, float[])>();
        foreach (var k in keys)
            if (db.TryGetValue(k, out var bucket)) pool.AddRange(bucket);
        return pool.Count > 0 ? pool : _cardDbAll!;
    }

    /// <summary>アート窓のヒストグラムをプールと照合し最近傍を返す（しきい値・マージンで採否）。</summary>
    Match? Identify(Bitmap frame, Rectangle art, List<(string Id, float[] Hist)> pool)
    {
        var q = HsvHistogram.Compute(frame, art);
        string? bestId = null; double best = double.MaxValue, second = double.MaxValue;
        foreach (var (id, h) in pool)
        {
            double d = HsvHistogram.ChiSquare(q, h);
            if (d < best) { second = best; best = d; bestId = id; }
            else if (d < second) second = d;
        }
        if (bestId is null || best > MaxDistance || (second - best) < MinMargin) return null;
        double conf = Math.Clamp((1.0 - best) * 0.5 + Math.Min(1.0, (second - best) / 0.3) * 0.5, 0, 1);
        return new Match(bestId, best, conf);
    }

    /// <summary>キャラ別 portrait DB を遅延構築する（<see cref="TemplateCardRecognizer"/> の構築を踏襲）。</summary>
    static readonly Dictionary<string, List<(string Id, float[] Hist)>> EmptyCardDb = new();

    Dictionary<string, List<(string Id, float[] Hist)>> EnsureCardDb()
    {
        if (_cardDb is not null) return _cardDb;
        var dir = PortraitsDir;
        if (dir is null) return EmptyCardDb; // 未セットアップ：空をキャッシュせず次フレームで再試行

        var db = new Dictionary<string, List<(string Id, float[] Hist)>>(StringComparer.Ordinal);
        var all = new List<(string Id, float[] Hist)>();
        foreach (var id in CardDatabaseService.GetAllCardIds())
        {
            var path = CardImageService.GetSourcePath(dir, id);
            if (path is null || !File.Exists(path)) continue;
            try
            {
                using var bmp = new Bitmap(path);
                int ph = Math.Max(1, (int)(bmp.Height * (1.0 - PortraitBottomTrim)));
                var hist = HsvHistogram.Compute(bmp, new Rectangle(0, 0, bmp.Width, ph));
                var key = CardDatabaseService.GetCardCharacter(id).ToUpperInvariant(); // "" = ニュートラル
                if (!db.TryGetValue(key, out var bucket))
                    db[key] = bucket = new();
                bucket.Add((id, hist));
                all.Add((id, hist));
            }
            catch { /* 壊れた画像はスキップ */ }
        }
        _cardDbAll = all;
        return _cardDb = db; // アセット解決後に初めて確定キャッシュ
    }

    static Rectangle ToPixels(Rectangle client, CardSlot slot)
    {
        int w = Math.Max(1, (int)(client.Width * slot.WFrac));
        int h = Math.Max(1, (int)(client.Height * slot.HFrac));
        int cx = client.X + (int)(client.Width * slot.CxFrac);
        int cy = client.Y + (int)(client.Height * slot.CyFrac);
        return new Rectangle(cx - w / 2, cy - h / 2, w, h);
    }

    static List<Rectangle> CardRects(Rectangle client, List<CardSlot> slots)
    {
        var list = new List<Rectangle>(slots.Count);
        foreach (var s in slots) list.Add(ToPixels(client, s));
        return list;
    }

    /// <summary>
    /// カード全体矩形（=枠色二値化の緑フレーム外接矩形と同ジオメトリ）からアート窓を導出する。
    /// <see cref="CardRegionDetector.ArtRegionOf"/>（カード幅基準・portrait DB のトリムと整合）に委譲。
    /// 固定スロットはこの矩形ジオメトリに合わせて較正してある。
    /// </summary>
    static Rectangle ArtWindow(Rectangle card) => CardRegionDetector.ArtRegionOf(card);

    void TrySaveCrop(Bitmap frame, Rectangle rect)
    {
        var dir = SaveCropsDir;
        if (dir is null) return;
        try
        {
            Directory.CreateDirectory(dir);
            int n = System.Threading.Interlocked.Increment(ref _cropSeq);
            using var crop = frame.Clone(rect, frame.PixelFormat);
            crop.Save(Path.Combine(dir, $"card_{n:D4}.png"), System.Drawing.Imaging.ImageFormat.Png);
        }
        catch { /* 保存失敗は無視 */ }
    }

    static string? ResolvePortraitsDir()
    {
        var candidate = AssetLocator.ImagesDir(CardImageService.PortraitsDirName);
        return candidate != null && Directory.Exists(candidate) ? candidate : null;
    }
}
