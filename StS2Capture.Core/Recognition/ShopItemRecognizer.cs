using StS2Shared.Services;

namespace StS2Capture.Recognition;

/// <summary>
/// ショップ画面の固定スロット（レリック行・ポーション行）を probe して、販売中のレリック／ポーションを
/// 識別する。座標はクライアント領域基準の相対値（解像度・タイトルバー有無に非依存）。
/// 識別は背景合成済み DB との HSV ヒストグラム照合（<see cref="HsvHistogram"/>）。
/// 強一致スロットが一定数あればショップ画面とみなす（probe-as-detector）。
/// 所有レリック（HUD 左上）はスロット座標に含めないため自然に除外される。
/// </summary>
public sealed class ShopItemRecognizer
{
    public enum Kind { Relic, Potion }

    public readonly record struct Slot(Kind Kind, double CxFrac, double CyFrac);

    /// <summary>スロットの識別候補（距離が近いほど確からしい）。</summary>
    public sealed record Candidate(string Id, string? Name, double Distance);

    /// <summary>
    /// 1 スロットの結果。<see cref="Candidates"/> は距離昇順。best が圏内なら採用し、best から
    /// タイ窓（<see cref="MinMargin"/>）以内の近接候補も併せて列挙する（同色僅差ペアの両方表示）。
    /// </summary>
    public sealed record Item(Kind Kind, Rectangle Region, IReadOnlyList<Candidate> Candidates, bool Accepted);

    public sealed record Result(bool IsShop, IReadOnlyList<Item> Items);

    /// <summary>DB の透過部分を合成するショップ背景色（羊皮紙）。</summary>
    public Color ShopBackground { get; set; } = Color.FromArgb(78, 118, 106);

    /// <summary>採否しきい値（実機で較正）。S4V4 ビン＋透過トリムでの実測に合わせた値。</summary>
    public double MaxDistance { get; set; } = 0.40;

    /// <summary>best からこの距離差以内の候補は「同点」とみなし併せて列挙する（タイ窓）。</summary>
    public double MinMargin { get; set; } = 0.02;

    /// <summary>1 スロットあたりの最大候補数（タイ窓内でも上限）。</summary>
    public int MaxCandidates { get; set; } = 3;

    /// <summary>HSV ヒストグラムの S/V ビン数（小アイコン識別のため既定 3 より細かく）。</summary>
    const int SatBins = 4, ValBins = 4;

    /// <summary>この数以上のスロットが強一致したらショップ画面とみなす。</summary>
    public int MinMatchesForShop { get; set; } = 2;

    /// <summary>スロット正方形の一辺（クライアント高さに対する比）。</summary>
    public double SlotSizeFrac { get; set; } = 0.072;

    /// <summary>非 null の間、各スロットの切り出しを PNG 保存する（実機較正用）。</summary>
    public string? SaveCropsDir { get; set; }
    static int _cropSeq;

    /// <summary>
    /// クライアント相対のスロット中心。実機フルフレーム（client 1276×718, 原点 (6,45)）でアイコン中心を
    /// 実測して較正した既定値。レリック行 yFrac≈0.6225、ポーション行 yFrac≈0.744、
    /// 列 xFrac≈0.518/0.5925/0.6693。クライアント相対なので解像度・全画面でも一致する想定。
    /// </summary>
    public List<Slot> Slots { get; set; } = new()
    {
        new(Kind.Relic, 0.518, 0.6225), new(Kind.Relic, 0.5925, 0.6225), new(Kind.Relic, 0.6693, 0.6225),
        new(Kind.Potion, 0.518, 0.744), new(Kind.Potion, 0.5925, 0.744), new(Kind.Potion, 0.6693, 0.744),
    };

    readonly string? _relicsDir = ResolveImagesDir(RelicImageService.RelicsDirName);
    readonly string? _potionsDir = ResolveImagesDir(PotionImageService.PotionsDirName);
    Dictionary<string, float[]>? _relicDb, _potionDb;

    public bool IsAvailable => _relicsDir is not null && _potionsDir is not null;

    public Result Detect(Bitmap frame, Rectangle client)
    {
        var relicDb = EnsureDb(ref _relicDb, _relicsDir,
            CardDatabaseService.GetAllRelicIds(), RelicImageService.GetSourcePath);
        var potionDb = EnsureDb(ref _potionDb, _potionsDir,
            PotionImageService.Ids, PotionImageService.GetSourcePath);

        var items = new List<Item>(Slots.Count);
        int side = Math.Max(8, (int)(client.Height * SlotSizeFrac));
        var frameRect = new Rectangle(0, 0, frame.Width, frame.Height);

        foreach (var slot in Slots)
        {
            int cx = client.X + (int)(client.Width * slot.CxFrac);
            int cy = client.Y + (int)(client.Height * slot.CyFrac);
            var rect = Rectangle.Intersect(
                new Rectangle(cx - side / 2, cy - side / 2, side, side), frameRect);
            if (rect.Width < 8 || rect.Height < 8) continue;

            TrySaveCrop(frame, rect);

            // クエリは実背景（羊皮紙）上にあるので合成なし。DB は背景合成済み。
            var q = HsvHistogram.Compute(frame, rect, null, SatBins, ValBins);
            var db = slot.Kind == Kind.Relic ? relicDb : potionDb;
            var cands = NearestCandidates(q, db, slot.Kind);

            items.Add(new Item(slot.Kind, rect, cands, cands.Count > 0));
        }

        bool isShop = items.Count(i => i.Accepted) >= MinMatchesForShop;
        return new Result(isShop, items);
    }

    static string NameOf(Kind kind, string id) =>
        kind == Kind.Relic
            ? CardDatabaseService.GetRelicTitle(id, japanese: true)
            : CardDatabaseService.GetPotionTitle(id, japanese: true);

    /// <summary>
    /// best が <see cref="MaxDistance"/> 以内なら、best と best からタイ窓（<see cref="MinMargin"/>）以内の
    /// 近接候補を距離昇順で最大 <see cref="MaxCandidates"/> 件返す。圏外なら空（不採用）。
    /// </summary>
    IReadOnlyList<Candidate> NearestCandidates(float[] q, Dictionary<string, float[]> db, Kind kind)
    {
        var scored = new List<(string Id, double D)>(db.Count);
        foreach (var (id, h) in db) scored.Add((id, HsvHistogram.ChiSquare(q, h)));
        scored.Sort((a, b) => a.D.CompareTo(b.D));

        var result = new List<Candidate>();
        if (scored.Count == 0 || scored[0].D > MaxDistance) return result;
        double best = scored[0].D;
        foreach (var (id, d) in scored)
        {
            if (d - best >= MinMargin || result.Count >= MaxCandidates) break;
            result.Add(new Candidate(id, NameOf(kind, id), d));
        }
        return result;
    }

    Dictionary<string, float[]> EnsureDb(ref Dictionary<string, float[]>? cache, string? dir,
        IEnumerable<string> ids, Func<string, string, string?> resolve)
    {
        if (cache is not null) return cache;
        cache = new(StringComparer.Ordinal);
        if (dir is null) return cache;
        foreach (var id in ids)
        {
            var path = resolve(dir, id);
            if (path is null || !File.Exists(path)) continue;
            try
            {
                using var bmp = new Bitmap(path);
                // 透過余白をトリムしてアートを枠いっぱいに → ショップ背景色で合成してから集計（照合成立の鍵）。
                cache[id] = HsvHistogram.Compute(bmp,
                    HsvHistogram.AlphaBoundingBox(bmp), ShopBackground, SatBins, ValBins);
            }
            catch { /* 壊れた画像はスキップ */ }
        }
        return cache;
    }

    void TrySaveCrop(Bitmap frame, Rectangle rect)
    {
        var dir = SaveCropsDir;
        if (dir is null) return;
        try
        {
            Directory.CreateDirectory(dir);
            int n = System.Threading.Interlocked.Increment(ref _cropSeq);
            using var crop = frame.Clone(rect, frame.PixelFormat);
            crop.Save(Path.Combine(dir, $"slot_{n:D4}.png"), System.Drawing.Imaging.ImageFormat.Png);
        }
        catch { /* 保存失敗は無視 */ }
    }

    static string? ResolveImagesDir(string dirName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "tools", "extracted", "images", dirName);
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }
}
