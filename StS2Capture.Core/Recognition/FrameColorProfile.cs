namespace StS2Capture.Recognition;

/// <summary>
/// カード枠色の判定プロファイル。ピクセル (r,g,b) が枠色かを返す述語と表示名を持つ。
/// キャラごとに実測した色（RGB しきい値）や、色相非依存の彩度リングなどを差し替えられる。
/// 矩形検出はカード内容（=キャラ）を知る前段なので、プロファイルは current_run.save から
/// 解決した現在キャラで選ぶ（<see cref="ForCharacter"/>）。
/// </summary>
public sealed class FrameColorProfile
{
    public string Name { get; }
    readonly Func<int, int, int, bool> _matches;

    /// <summary>カード形状フィルタの充填率しきい値（連結成分のリング状の密度）。プロファイル単位。</summary>
    public double MinFill { get; }
    public double MaxFill { get; }

    public FrameColorProfile(string name, Func<int, int, int, bool> matches,
        double minFill = 0.015, double maxFill = 0.65)
    {
        Name = name;
        _matches = matches;
        MinFill = minFill;
        MaxFill = maxFill;
    }

    /// <summary>(r,g,b) が枠色か。</summary>
    public bool Matches(int r, int g, int b) => _matches(r, g, b);

    /// <summary>
    /// Defect の実機計測に合わせた青枠プロファイル（検証済み挙動）。
    /// 実測枠は B が突出した暗いティール青（例 RGB(18,91,129)〜(31,108,148)）。
    /// </summary>
    public static FrameColorProfile DefectBlue { get; } = new(
        "Defect(青・実測)",
        static (r, g, b) => b >= 95 && (b - r) >= 50 && (b - g) >= 14 && r <= 80);

    /// <summary>
    /// Ironclad の実機計測に合わせたカード外枠プロファイル。外枠は濃い赤茶（≈ RGB(137,54,37)）。
    /// 明るい純赤の絵（R が高い）・レアリティのシアン枠（G/B が高い）・グレー（R-G 小）を除外し、
    /// 外枠の細リングだけを拾う（提供スクショで3枚とも低 fill で検出を確認）。
    /// </summary>
    public static FrameColorProfile IroncladRed { get; } = new(
        "Ironclad(赤茶・実測)",
        static (r, g, b) =>
            r >= 90 && r <= 150 && g >= 38 && g <= 80 && b >= 22 && b <= 60 &&
            (r - g) >= 55 && (r - g) <= 105 && (r - b) >= 60);

    /// <summary>
    /// Silent の実機計測に合わせたカード外枠プロファイル。外枠はくすんだ緑（≈ RGB(68,116,33)）。
    /// 明るい黄緑の絵（G が高い）・レアリティのシアン枠（G≈B）・青/紫/グレーを除外し、外枠の
    /// 細リングだけを拾う（提供スクショで3枚とも低 fill で検出を確認）。
    /// </summary>
    public static FrameColorProfile SilentGreen { get; } = new(
        "Silent(緑・実測)",
        static (r, g, b) =>
            (g - r) >= 30 && (g - b) >= 50 && g >= 80 && g <= 140 &&
            r >= 30 && r <= 95 && b >= 12 && b <= 55);

    /// <summary>
    /// Necrobinder の実機計測に合わせたカード外枠プロファイル。外枠はくすんだ赤紫／ローズ
    /// （≈ RGB(142,66,94)、B が G より高いのが特徴）。紫の絵（B≥R）・茶色の絵（B≤G）・
    /// レアリティのシアン枠・グレーを除外し、外枠の細リングだけを拾う
    /// （提供スクショで3枚とも低 fill で検出を確認）。
    /// </summary>
    public static FrameColorProfile NecrobinderRose { get; } = new(
        "Necrobinder(赤紫・実測)",
        static (r, g, b) =>
            (r - g) >= 50 && (r - g) <= 95 && (b - g) >= 12 &&
            (r - b) >= 20 && (r - b) <= 70 &&
            r >= 95 && r <= 170 && g >= 40 && g <= 90 && b >= 55 && b <= 120);

    /// <summary>
    /// Regent の実機計測に合わせたカード外枠プロファイル。外枠は鮮やかなオレンジ
    /// （≈ RGB(171,90,0)、B がほぼ 0）。紫/マゼンタの絵（B が高い）・シアン・黄緑・グレーを除外し、
    /// 外枠の細リングだけを拾う（提供スクショで3枚とも低 fill で検出を確認）。
    /// </summary>
    public static FrameColorProfile RegentOrange { get; } = new(
        "Regent(橙・実測)",
        static (r, g, b) =>
            (r - g) >= 50 && (r - g) <= 120 && (g - b) >= 40 && b <= 35 &&
            r >= 130 && r <= 215 && g >= 50 && g <= 130);

    /// <summary>
    /// colorless カードの実機計測に合わせたカード外枠プロファイル。外枠はニュートラルグレー
    /// （≈ RGB(92,92,92)、無彩色・中明度）。キャラ非依存で、ショップ／報酬等の colorless カードを
    /// キャラ枠と同時に拾うために使う。下部テキスト台もグレーで一緒に拾うため fill が高めになる
    /// ので <see cref="MaxFill"/> を 0.80 に緩める（提供スクショで2枚とも検出・誤検出なしを確認）。
    /// </summary>
    public static FrameColorProfile ColorlessGray { get; } = new(
        "Colorless(灰・実測)",
        static (r, g, b) =>
        {
            int max = Math.Max(r, Math.Max(g, b));
            int min = Math.Min(r, Math.Min(g, b));
            return (max - min) <= 16 && max >= 70 && max <= 135;
        },
        maxFill: 0.80);

    /// <summary>
    /// 色相非依存の彩度リング。彩度 S が高く・明度 V が中程度以上のピクセルを枠候補にする。
    /// 未実測キャラ／セーブ未検出時のベストエフォート。枠は細い均一色リング（低 fill）で
    /// 形状フィルタを通り、塗り潰しの絵（高 fill）や暗い背景（低 S/V）は除外される前提。
    /// HSV は手計算（<see cref="ImageOps.RowSaturation"/> と同じ max/min 式）。
    /// </summary>
    public static FrameColorProfile SaturatedRing { get; } = new(
        "汎用(彩度リング)",
        static (r, g, b) =>
        {
            int max = Math.Max(r, Math.Max(g, b));
            int min = Math.Min(r, Math.Min(g, b));
            if (max == 0) return false;
            double s = (double)(max - min) / max;
            double v = max / 255.0;
            return s >= 0.30 && v >= 0.18;
        },
        // 色相非依存ゆえ彩度の高い絵で内部が埋まる。塗り潰しカードを許容するため MaxFill を上げる。
        maxFill: 0.85);

    /// <summary>キャラ正規化 ID（大文字、例 "DEFECT"）→ 実測プロファイル。未登録は彩度リングに落ちる。</summary>
    static readonly Dictionary<string, FrameColorProfile> Measured = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DEFECT"] = DefectBlue,
        ["IRONCLAD"] = IroncladRed,
        ["SILENT"] = SilentGreen,
        ["NECROBINDER"] = NecrobinderRose,
        ["REGENT"] = RegentOrange,
    };

    /// <summary>実測プロファイルを持つキャラ ID の一覧（UI の手動上書き候補に使う）。</summary>
    public static IReadOnlyCollection<string> MeasuredCharacters => Measured.Keys;

    /// <summary>
    /// キャラ ID に対応するプロファイルを返す。null/未登録キャラは彩度リング・フォールバック。
    /// </summary>
    public static FrameColorProfile ForCharacter(string? characterId)
    {
        if (characterId is not null && Measured.TryGetValue(characterId, out var p))
            return p;
        return SaturatedRing;
    }
}
