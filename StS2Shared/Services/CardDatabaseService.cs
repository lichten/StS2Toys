using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace StS2Shared.Services;

public static class CardDatabaseService
{
    record Entry(string En, string Ja);

    static readonly Dictionary<string, Entry> _db = Load();
    static readonly Dictionary<string, string> _types = LoadTypes();
    static readonly Dictionary<string, string> _rarities = LoadStringDict("card_rarities.json");
    static readonly Dictionary<string, string> _characters = LoadStringDict("card_characters.json");
    static readonly Dictionary<string, int> _costs = LoadCosts();

    static Dictionary<string, Entry> Load()
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames()
            .First(n => n.EndsWith("card_database.json"));

        using var stream = asm.GetManifestResourceStream(name)!;
        var doc = JsonDocument.Parse(stream);

        var result = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            var en = prop.Value.GetProperty("en").GetString() ?? "";
            var ja = prop.Value.GetProperty("ja").GetString() ?? en;
            result[prop.Name] = new Entry(en, ja);
        }
        return result;
    }

    static Dictionary<string, string> LoadTypes()
        => LoadStringDict("card_types.json");

    static Dictionary<string, string> LoadStringDict(string suffix)
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(suffix));
        if (name is null) return new Dictionary<string, string>();

        using var stream = asm.GetManifestResourceStream(name)!;
        var doc = JsonDocument.Parse(stream);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in doc.RootElement.EnumerateObject())
            result[prop.Name] = prop.Value.GetString() ?? "";
        return result;
    }

    static Dictionary<string, int> LoadCosts()
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("card_costs.json"));
        if (name is null) return new Dictionary<string, int>();

        using var stream = asm.GetManifestResourceStream(name)!;
        var doc = JsonDocument.Parse(stream);
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in doc.RootElement.EnumerateObject())
            result[prop.Name] = prop.Value.GetInt32();
        return result;
    }

    public static string GetCardType(string id)
    {
        _types.TryGetValue(id, out var type);
        return type ?? "";
    }

    public static string GetCardRarity(string id)
    {
        _rarities.TryGetValue(id, out var rarity);
        return rarity ?? "";
    }

    public static string GetCardCharacter(string id)
    {
        _characters.TryGetValue(id, out var character);
        return character ?? "";
    }

    public static string GetCardCost(string id)
    {
        if (!_costs.TryGetValue(id, out var cost)) return "";
        if (cost != -1) return cost.ToString();
        var type = GetCardType(id);
        return type is "Attack" or "Skill" or "Power" ? "X" : "-";
    }

    public static string GetName(string id, bool japanese = false)
    {
        if (_db.TryGetValue(id, out var entry))
            return japanese ? entry.Ja : entry.En;

        // フォールバック: CARD./RELIC. を除いてタイトルケースで返す
        var raw = id.Contains('.') ? id[(id.IndexOf('.') + 1)..] : id;
        return ToTitleCase(raw.Replace('_', ' '));
    }

    /// <summary>
    /// レリック ID のローカライズタイトルを返す。
    /// 見つからない場合はタイトルケースにフォールバック。
    /// </summary>
    public static string GetRelicTitle(string id, bool japanese = false)
    {
        var loc = japanese ? _loc.JpnRelics : _loc.EngRelics;
        var key = id + ".title";
        if (loc.TryGetValue(key, out var title) && !string.IsNullOrWhiteSpace(title))
            return title;
        return ToTitleCase(id.Replace('_', ' '));
    }

    static string ToTitleCase(string s) =>
        string.Join(' ', s.Split(' ')
            .Select(w => w.Length == 0 ? w : char.ToUpper(w[0]) + w[1..].ToLower()));

    // ---- localization (description / flavor) ----

    record LocData(
        IReadOnlyDictionary<string, string> EngCards,
        IReadOnlyDictionary<string, string> JpnCards,
        IReadOnlyDictionary<string, string> EngRelics,
        IReadOnlyDictionary<string, string> JpnRelics);

    static readonly LocData _loc = LoadLoc();
    static readonly HashSet<string> _blockGivers = ComputeBlockGivers();
    static readonly HashSet<string> _blockRelicGivers = ComputeBlockRelicGivers();
    static readonly HashSet<string> _drawRelated = ComputeDrawRelated();
    static readonly HashSet<string> _drawRelicRelated = ComputeDrawRelicRelated();
    static readonly HashSet<string> _necroOsty     = ComputeByTag("[gold]Osty[/gold]", "[gold]Osty's[/gold]");
    static readonly HashSet<string> _necroSoul     = ComputeByTag("[gold]Soul[/gold]");
    static readonly HashSet<string> _necroDoom     = ComputeByTag("[gold]Doom[/gold]");
    static readonly HashSet<string> _necroSummon   = ComputeByTag("[gold]Summon[/gold]");
    static readonly HashSet<string> _ironcladStr    = ComputeByTag("[gold]Strength[/gold]");
    static readonly HashSet<string> _ironcladEx    = ComputeByTag("[gold]Exhaust[/gold]", "[gold]Exhausted[/gold]", "[gold]Exhaust Pile[/gold]");
    static readonly HashSet<string> _ironcladStrike = ComputeByNameContaining("Strike");
    static readonly HashSet<string> _silentPoison  = ComputeByTag("[gold]Poison[/gold]");
    static readonly HashSet<string> _silentShiv    = ComputeByTag("[gold]Shiv[/gold]", "[gold]Shivs[/gold]");
    static readonly HashSet<string> _defectChannel = ComputeByTag("[gold]Channel[/gold]", "[gold]Channeled[/gold]", "[gold]Channels[/gold]");
    static readonly HashSet<string> _defectEvoke   = ComputeByTag("[gold]Evoke[/gold]");
    static readonly HashSet<string> _defectFocus   = ComputeByTag("[gold]Focus[/gold]");
    static readonly HashSet<string> _weak           = ComputeByTag("[gold]Weak[/gold]");
    static readonly HashSet<string> _vulnerable    = ComputeByTag("[gold]Vulnerable[/gold]");
    static readonly HashSet<string> _regentForge   = ComputeByTag("[gold]Forge[/gold]", "[gold]Forges[/gold]");
    static readonly HashSet<string> _regentBlade   = ComputeByTag("[gold]Sovereign Blade[/gold]");
    static readonly HashSet<string> _regentCreate     = ComputeByPlainText("Whenever you create", "created this combat");
    static readonly HashSet<string> _regentStatusGen  = ComputeStatusGenerators();
    static readonly HashSet<string> _regentTransform  = ComputeByTag("[gold]Transform[/gold]");
    static readonly HashSet<string> _regentStarGain   = ComputeRegentStarGain();
    static readonly HashSet<string> _regentStarSpend  = ComputeRegentStarSpend();
    static readonly HashSet<string> _defectZeroEnergy = BuildDefectZeroEnergy();

    static HashSet<string> ComputeBlockGivers()
    {
        const string blockTag   = "[gold]Block[/gold]";
        const string channelTag = "[gold]Channel[/gold]";
        const string frostTag   = "[gold]Frost[/gold]";
        const string platingTag = "[gold]Plating[/gold]";
        const string descSuffix = ".description";
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, desc) in _loc.EngCards)
        {
            if (!key.EndsWith(descSuffix, StringComparison.Ordinal)) continue;
            // ブロック直接付与・倍増
            bool isBlockGiver = desc.Contains(blockTag, StringComparison.Ordinal) &&
                (desc.Contains("gain",   StringComparison.OrdinalIgnoreCase) ||
                 desc.Contains("Double", StringComparison.OrdinalIgnoreCase));
            // Frostオーブ生成（ディフェクトのブロック源）
            bool isFrostChanneler = desc.Contains(channelTag, StringComparison.Ordinal) &&
                                    desc.Contains(frostTag,   StringComparison.Ordinal);
            // プレート付与（ターン終了時にブロックを得るバフ）
            bool isPlatingGiver = desc.Contains(platingTag, StringComparison.Ordinal) &&
                                  desc.Contains("gain", StringComparison.OrdinalIgnoreCase);
            if (isBlockGiver || isFrostChanneler || isPlatingGiver)
                result.Add(key[..^descSuffix.Length]);
        }
        return result;
    }

    static HashSet<string> ComputeBlockRelicGivers()
    {
        const string blockTag   = "[gold]Block[/gold]";
        const string platingTag = "[gold]Plating[/gold]";
        const string descSuffix = ".description";
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, desc) in _loc.EngRelics)
        {
            if (!key.EndsWith(descSuffix, StringComparison.Ordinal)) continue;
            // Rule 1: "gain [blue]{VALUE}[/blue] [gold]Block[/gold]" — 直接ブロック付与
            // Rule 2: "each combat" — 戦闘開始時付与（ANCHOR 等）＋ VAMBRACE
            // Rule 3: "double" — ブロック倍増（PAELS_LEGION, VITRUVIAN_MINION 等）
            bool isBlockGiver = desc.Contains(blockTag, StringComparison.Ordinal) &&
                (desc.Contains("gain [blue]{", StringComparison.Ordinal) ||
                 desc.Contains("each combat",  StringComparison.OrdinalIgnoreCase) ||
                 desc.Contains("double",       StringComparison.OrdinalIgnoreCase));
            // Rule 4: プレート付与（GORGET 等）
            bool isPlatingGiver = desc.Contains(platingTag, StringComparison.Ordinal) &&
                                  desc.Contains("gain", StringComparison.OrdinalIgnoreCase);
            if (isBlockGiver || isPlatingGiver)
                result.Add(key[..^descSuffix.Length]);
        }
        return result;
    }

    static HashSet<string> ComputeDrawRelated()
    {
        const string drawPileTag = "[gold]Draw Pile[/gold]";
        const string descSuffix = ".description";
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, desc) in _loc.EngCards)
        {
            if (!key.EndsWith(descSuffix, StringComparison.Ordinal)) continue;
            var stripped = desc.Replace(drawPileTag, "", StringComparison.Ordinal);
            bool isDrawCard = stripped.Contains("draw", StringComparison.OrdinalIgnoreCase);
            // "Add ... Soul/Souls" パターン: Soulカードを生成してドローできるカード
            bool isSoulGenerator = desc.Contains("Add", StringComparison.OrdinalIgnoreCase) &&
                                   desc.Contains("Soul", StringComparison.OrdinalIgnoreCase);
            if (isDrawCard || isSoulGenerator)
                result.Add(key[..^descSuffix.Length]);
        }
        return result;
    }

    static HashSet<string> ComputeDrawRelicRelated()
    {
        const string drawPileTag = "[gold]Draw Pile[/gold]";
        const string descSuffix = ".description";
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, desc) in _loc.EngRelics)
        {
            if (!key.EndsWith(descSuffix, StringComparison.Ordinal)) continue;
            var stripped = desc.Replace(drawPileTag, "", StringComparison.Ordinal);
            if (stripped.Contains("draw", StringComparison.OrdinalIgnoreCase))
                result.Add(key[..^descSuffix.Length]);
        }
        return result;
    }

    static HashSet<string> ComputeByTag(params string[] tags)
    {
        const string descSuffix = ".description";
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, desc) in _loc.EngCards)
        {
            if (!key.EndsWith(descSuffix, StringComparison.Ordinal)) continue;
            if (tags.Any(t => desc.Contains(t, StringComparison.Ordinal)))
                result.Add(key[..^descSuffix.Length]);
        }
        return result;
    }

    static HashSet<string> ComputeByPlainText(params string[] phrases)
    {
        const string descSuffix = ".description";
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, desc) in _loc.EngCards)
        {
            if (!key.EndsWith(descSuffix, StringComparison.Ordinal)) continue;
            if (phrases.Any(p => desc.Contains(p, StringComparison.OrdinalIgnoreCase)))
                result.Add(key[..^descSuffix.Length]);
        }
        return result;
    }

    static HashSet<string> ComputeByNameContaining(string substring)
    {
        const string titleSuffix = ".title";
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in _loc.EngCards)
        {
            if (!key.EndsWith(titleSuffix, StringComparison.Ordinal)) continue;
            if (value.Contains(substring, StringComparison.OrdinalIgnoreCase))
                result.Add(key[..^titleSuffix.Length]);
        }
        return result;
    }

    static HashSet<string> ComputeRegentStarGain()
    {
        var result = ComputeByPlainText("starIcons()", "Gain {singleStarIcon}", "gain {singleStarIcon}");
        result.Add("ROYAL_GAMBLE"); // "Gain {Stars:diff()} {singleStarIcon}" — 既存パターン非対応のため手動追加
        return result;
    }

    static HashSet<string> ComputeRegentStarSpend()
    {
        var result = ComputeByPlainText(
            "spend {singleStarIcon}", "spend or gain {singleStarIcon}",
            "{singleStarIcon} are used", "{singleStarIcon} cost", "{StarThreshold");
        // Starをマナコストとして消費するカードを追加（card_star_costs.json から）
        foreach (var id in LoadStarCostIds())
            result.Add(id);
        return result;
    }

    static IEnumerable<string> LoadStarCostIds()
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("card_star_costs.json", StringComparison.OrdinalIgnoreCase));
        if (name is null) yield break;

        using var stream = asm.GetManifestResourceStream(name)!;
        var doc = JsonDocument.Parse(stream);
        foreach (var elem in doc.RootElement.EnumerateArray())
        {
            var id = elem.GetString();
            if (!string.IsNullOrEmpty(id))
                yield return ToRawId(id);
        }
    }

    static HashSet<string> BuildDefectZeroEnergy()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // コスト0ではないがシナジーカード
            "SCRAPE", "ALL_FOR_ONE", "FERAL", "ADAPTIVE_STRIKE", "MOMENTUM_STRIKE"
        };
        foreach (var (key, cost) in _costs)
            if (cost == 0 && _characters.TryGetValue(key, out var ch) && ch == "Defect")
                set.Add(ToRawId(key));
        return set;
    }

    static HashSet<string> ComputeStatusGenerators()
    {
        // card_types.json の Status 型カード名から [gold]{Name}[/gold] タグを構築
        var statusTags = _types
            .Where(kv => kv.Value.Equals("Status", StringComparison.OrdinalIgnoreCase))
            .Select(kv => ToTitleCase(ToRawId(kv.Key).Replace('_', ' ')))
            .SelectMany(name => new[] { $"[gold]{name}[/gold]", $"[gold]{name}s[/gold]" })
            .ToArray();

        const string descSuffix = ".description";
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, desc) in _loc.EngCards)
        {
            if (!key.EndsWith(descSuffix, StringComparison.Ordinal)) continue;
            if (!desc.Contains("Add", StringComparison.Ordinal)) continue;
            if (statusTags.Any(tag => desc.Contains(tag, StringComparison.OrdinalIgnoreCase)))
                result.Add(key[..^descSuffix.Length]);
        }
        return result;
    }

    public static bool IsWeak(string id)           => _weak.Contains(ToRawId(id));
    public static bool IsVulnerable(string id)    => _vulnerable.Contains(ToRawId(id));
    public static bool IsNecroOsty(string id)      => _necroOsty.Contains(ToRawId(id));
    public static bool IsNecroSoul(string id)      => _necroSoul.Contains(ToRawId(id));
    public static bool IsNecroDoom(string id)      => _necroDoom.Contains(ToRawId(id));
    public static bool IsNecroSummon(string id)    => _necroSummon.Contains(ToRawId(id));
    public static bool IsIroncladStrength(string id) => _ironcladStr.Contains(ToRawId(id));
    public static bool IsIroncladExhaust(string id)  => _ironcladEx.Contains(ToRawId(id));
    public static bool IsIroncladStrike(string id)   => _ironcladStrike.Contains(ToRawId(id));
    public static bool IsSilentPoison(string id)   => _silentPoison.Contains(ToRawId(id));
    public static bool IsSilentShiv(string id)     => _silentShiv.Contains(ToRawId(id));
    public static bool IsDefectChannel(string id)     => _defectChannel.Contains(ToRawId(id));
    public static bool IsDefectEvoke(string id)       => _defectEvoke.Contains(ToRawId(id));
    public static bool IsDefectFocus(string id)       => _defectFocus.Contains(ToRawId(id));
    public static bool IsDefectZeroEnergy(string id)  => _defectZeroEnergy.Contains(ToRawId(id));
    public static bool IsRegentForge(string id)     => _regentForge.Contains(ToRawId(id));
    public static bool IsRegentBlade(string id)     => _regentBlade.Contains(ToRawId(id));
    public static bool IsRegentStarGain(string id)  => _regentStarGain.Contains(ToRawId(id));
    public static bool IsRegentStarSpend(string id) => _regentStarSpend.Contains(ToRawId(id));
    static readonly HashSet<string> _regentCreateExtra = new(StringComparer.OrdinalIgnoreCase) { "METAMORPHOSIS", "SPECTRUM_SHIFT" };
    public static bool IsRegentCreate(string id)   => _regentCreate.Contains(ToRawId(id))
                                                    || _regentStatusGen.Contains(ToRawId(id))
                                                    || _regentTransform.Contains(ToRawId(id))
                                                    || _regentCreateExtra.Contains(ToRawId(id));

    public static bool IsBlockGiver(string id) => _blockGivers.Contains(ToRawId(id));
    public static bool IsRelicBlockGiver(string id) => _blockRelicGivers.Contains(ToRawId(id));
    public static bool IsDrawRelated(string id) => _drawRelated.Contains(ToRawId(id));
    public static bool IsRelicDrawRelated(string id) => _drawRelicRelated.Contains(ToRawId(id));

    static LocData LoadLoc() => new(
        LoadLocJson("eng.cards"),
        LoadLocJson("jpn.cards"),
        LoadLocJson("eng.relics"),
        LoadLocJson("jpn.relics"));

    static IReadOnlyDictionary<string, string> LoadLocJson(string suffix)
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith($"localization.{suffix}.json",
                                 StringComparison.OrdinalIgnoreCase));
        if (name is null) return new Dictionary<string, string>();

        using var stream = asm.GetManifestResourceStream(name)!;
        var doc = JsonDocument.Parse(stream);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in doc.RootElement.EnumerateObject())
            result[prop.Name] = prop.Value.GetString() ?? "";
        return result;
    }

    static string ToRawId(string id) =>
        id.Contains('.') ? id[(id.IndexOf('.') + 1)..] : id;

    // ---- enchantment localization ----

    static readonly IReadOnlyDictionary<string, string> _enchantEng = LoadLocJson("eng.enchantments");
    static readonly IReadOnlyDictionary<string, string> _enchantJpn = LoadLocJson("jpn.enchantments");

    // ---- card stats (card_stats.json) ----

    static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> _stats = LoadStats();

    static IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> LoadStats()
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("card_stats.json", StringComparison.OrdinalIgnoreCase));
        if (name is null) return new Dictionary<string, IReadOnlyDictionary<string, int>>();

        using var stream = asm.GetManifestResourceStream(name)!;
        var doc = JsonDocument.Parse(stream);
        var result = new Dictionary<string, IReadOnlyDictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        foreach (var card in doc.RootElement.EnumerateObject())
        {
            var fields = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in card.Value.EnumerateObject())
                fields[field.Name] = field.Value.GetInt32();
            result[ToRawId(card.Name)] = fields;
        }
        return result;
    }

    public static IReadOnlyDictionary<string, int>? GetCardStats(string id) =>
        _stats.TryGetValue(ToRawId(id), out var v) ? v : null;

    public static IEnumerable<string> GetAllCardIds() => _types.Keys;

    public static IEnumerable<string> GetAllRelicIds()
    {
        const string titleSuffix = ".title";
        return _loc.EngRelics.Keys
            .Where(k => k.EndsWith(titleSuffix, StringComparison.Ordinal))
            .Select(k => k[..^titleSuffix.Length])
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase);
    }

    // ---- relic rarities / stats ----

    static readonly IReadOnlyDictionary<string, string> _relicRarities = LoadStringDict("relic_rarities.json");
    static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> _relicStats = LoadRelicStats();

    static IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> LoadRelicStats()
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("relic_stats.json", StringComparison.OrdinalIgnoreCase));
        if (name is null) return new Dictionary<string, IReadOnlyDictionary<string, int>>();

        using var stream = asm.GetManifestResourceStream(name)!;
        var doc = JsonDocument.Parse(stream);
        var result = new Dictionary<string, IReadOnlyDictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        foreach (var relic in doc.RootElement.EnumerateObject())
        {
            var fields = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in relic.Value.EnumerateObject())
                fields[field.Name] = field.Value.GetInt32();
            result[relic.Name] = fields;
        }
        return result;
    }

    public static string GetRelicRarity(string id) =>
        _relicRarities.TryGetValue(id, out var r) ? r : "";

    public static IReadOnlyDictionary<string, int>? GetRelicStats(string id) =>
        _relicStats.TryGetValue(id, out var v) ? v : null;

    public static string GetEnchantmentName(string id, bool japanese)
    {
        var raw = ToRawId(id);
        var dict = japanese ? _enchantJpn : _enchantEng;
        return dict.TryGetValue($"{raw}.title", out var v) ? v : ToTitleCase(raw.Replace('_', ' '));
    }

    public static string FormatEnchantmentLabel(string id, int amount, bool japanese)
    {
        if (string.IsNullOrEmpty(id)) return "";
        var name = GetEnchantmentName(id, japanese);
        return amount > 0 ? $"{name} +{amount}" : name;
    }

    public static string GetEnchantmentDescription(string id, int amount, bool japanese)
    {
        if (string.IsNullOrEmpty(id)) return "";
        var raw = ToRawId(id);
        var dict = japanese ? _enchantJpn : _enchantEng;
        if (!dict.TryGetValue($"{raw}.description", out var desc) || string.IsNullOrEmpty(desc))
            return "";
        return DescriptionFormatter.CleanWithAmount(desc, amount, japanese);
    }

    public static (string En, string Ja) GetDescription(string id)
    {
        bool isRelic = id.StartsWith("RELIC.", StringComparison.OrdinalIgnoreCase);
        var key = ToRawId(id) + ".description";
        var eng = isRelic ? _loc.EngRelics : _loc.EngCards;
        var jpn = isRelic ? _loc.JpnRelics : _loc.JpnCards;
        var en = eng.TryGetValue(key, out var ev) ? ev : "";
        var ja = jpn.TryGetValue(key, out var jv) ? jv : en;
        return (en, ja);
    }

    public static (string En, string Ja)? GetFlavor(string id)
    {
        if (!id.StartsWith("RELIC.", StringComparison.OrdinalIgnoreCase)) return null;
        var key = ToRawId(id) + ".flavor";
        var en = _loc.EngRelics.TryGetValue(key, out var ev) ? ev : "";
        if (string.IsNullOrEmpty(en)) return null;
        var enClean = DescriptionFormatter.Clean(en);
        if (enClean.Contains("revealed", StringComparison.OrdinalIgnoreCase)) return null;
        var ja = _loc.JpnRelics.TryGetValue(key, out var jv) ? jv : en;
        return (enClean, DescriptionFormatter.Clean(ja, japanese: true));
    }
}
