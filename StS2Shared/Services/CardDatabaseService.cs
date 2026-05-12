using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace StS2Shared.Services;

public static class CardDatabaseService
{
    record Entry(string En, string Ja);

    static readonly Dictionary<string, Entry> _db = Load();
    static readonly Dictionary<string, string> _types = LoadTypes();
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
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("card_types.json"));
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
    static readonly HashSet<string> _ironcladStr   = ComputeByTag("[gold]Strength[/gold]");
    static readonly HashSet<string> _ironcladEx    = ComputeByTag("[gold]Exhaust[/gold]", "[gold]Exhausted[/gold]", "[gold]Exhaust Pile[/gold]");
    static readonly HashSet<string> _silentPoison  = ComputeByTag("[gold]Poison[/gold]");
    static readonly HashSet<string> _silentShiv    = ComputeByTag("[gold]Shiv[/gold]", "[gold]Shivs[/gold]");
    static readonly HashSet<string> _defectChannel = ComputeByTag("[gold]Channel[/gold]", "[gold]Channeled[/gold]", "[gold]Channels[/gold]");
    static readonly HashSet<string> _defectEvoke   = ComputeByTag("[gold]Evoke[/gold]");
    static readonly HashSet<string> _defectFocus   = ComputeByTag("[gold]Focus[/gold]");
    static readonly HashSet<string> _regentForge   = ComputeByTag("[gold]Forge[/gold]", "[gold]Forges[/gold]");
    static readonly HashSet<string> _regentBlade   = ComputeByTag("[gold]Sovereign Blade[/gold]");
    static readonly HashSet<string> _regentCreate     = ComputeByPlainText("Whenever you create", "created this combat");
    static readonly HashSet<string> _regentStatusGen  = ComputeStatusGenerators();
    static readonly HashSet<string> _regentTransform  = ComputeByTag("[gold]Transform[/gold]");

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
            if (desc.Contains("draw", StringComparison.OrdinalIgnoreCase) ||
                desc.Contains(drawPileTag, StringComparison.Ordinal))
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
            if (desc.Contains("draw", StringComparison.OrdinalIgnoreCase) ||
                desc.Contains(drawPileTag, StringComparison.Ordinal))
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

    public static bool IsNecroOsty(string id)      => _necroOsty.Contains(ToRawId(id));
    public static bool IsNecroSoul(string id)      => _necroSoul.Contains(ToRawId(id));
    public static bool IsNecroDoom(string id)      => _necroDoom.Contains(ToRawId(id));
    public static bool IsIroncladStrength(string id) => _ironcladStr.Contains(ToRawId(id));
    public static bool IsIroncladExhaust(string id)  => _ironcladEx.Contains(ToRawId(id));
    public static bool IsSilentPoison(string id)   => _silentPoison.Contains(ToRawId(id));
    public static bool IsSilentShiv(string id)     => _silentShiv.Contains(ToRawId(id));
    public static bool IsDefectChannel(string id)  => _defectChannel.Contains(ToRawId(id));
    public static bool IsDefectEvoke(string id)    => _defectEvoke.Contains(ToRawId(id));
    public static bool IsDefectFocus(string id)    => _defectFocus.Contains(ToRawId(id));
    public static bool IsRegentForge(string id)    => _regentForge.Contains(ToRawId(id));
    public static bool IsRegentBlade(string id)    => _regentBlade.Contains(ToRawId(id));
    static readonly HashSet<string> _regentCreateExtra = new(StringComparer.OrdinalIgnoreCase) { "METAMORPHOSIS" };
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
        return DescriptionFormatter.CleanWithAmount(desc, amount);
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
        return (enClean, DescriptionFormatter.Clean(ja));
    }
}
