using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace StS2Shared.Services;

public static class CardDatabaseService
{
    record Entry(string En, string Ja);

    static readonly Dictionary<string, Entry> _db = Load();
    static readonly Dictionary<string, Entry> _potionDb = LoadEntries("potion_database.json");
    static readonly Dictionary<string, string> _types = LoadTypes();
    static readonly Dictionary<string, string> _rarities = LoadStringDict("card_rarities.json");
    static readonly Dictionary<string, string> _characters = LoadStringDict("card_characters.json");
    static readonly Dictionary<string, int> _costs = LoadIntDict("card_costs.json");
    static readonly Dictionary<string, int> _upgradedCosts = LoadIntDict("card_upgraded_costs.json");

    static Dictionary<string, Entry> Load() => LoadEntries("card_database.json");

    /// <summary>{ID}→{en,ja} 形式の DB（card_database.json / potion_database.json）を読む。</summary>
    static Dictionary<string, Entry> LoadEntries(string suffix)
    {
        var asm = Assembly.GetExecutingAssembly();
        var result = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        using var stream = ResourceResolver.OpenText(asm, suffix);
        if (stream is null) return result;

        var doc = JsonDocument.Parse(stream);

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
        var name = ResourceResolver.ResolveVersioned(asm, suffix);
        if (name is null) return new Dictionary<string, string>();

        using var stream = asm.GetManifestResourceStream(name)!;
        var doc = JsonDocument.Parse(stream);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in doc.RootElement.EnumerateObject())
            result[prop.Name] = prop.Value.GetString() ?? "";
        return result;
    }

    static Dictionary<string, int> LoadIntDict(string fileName)
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = ResourceResolver.ResolveVersioned(asm, fileName);
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
        return FormatCost(id, cost);
    }

    /// <summary>
    /// アップグレードでコストが変わるカードのアップグレード後コスト値。変わらない場合は null。
    /// </summary>
    public static int? GetUpgradedCostValue(string id) =>
        _upgradedCosts.TryGetValue(id, out var c) ? c : null;

    /// <summary>
    /// アップグレード後コストの整形文字列（<see cref="GetCardCost"/> と同じ形式）。
    /// コストが変わらないカードは ""。
    /// </summary>
    public static string GetUpgradedCost(string id) =>
        _upgradedCosts.TryGetValue(id, out var c) ? FormatCost(id, c) : "";

    static string FormatCost(string id, int cost)
    {
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
        // ローカライズのキーは接頭辞なし（"AKABEKO.title"）。セーブ等の ID は接頭辞付き
        // （"RELIC.AKABEKO"）で来るため、そのまま／接頭辞除去の両方で引く。
        var raw = ToRawId(id);
        if (loc.TryGetValue(id + ".title", out var title) && !string.IsNullOrWhiteSpace(title))
            return title;
        if (loc.TryGetValue(raw + ".title", out var rawTitle) && !string.IsNullOrWhiteSpace(rawTitle))
            return rawTitle;
        return ToTitleCase(raw.Replace('_', ' '));
    }

    /// <summary>
    /// ポーション ID の表示名（potion_database.json 由来の EN/JP）。接頭辞 "POTION." は付いていても無くても可。
    /// 見つからない場合はタイトルケースにフォールバック。
    /// </summary>
    public static string GetPotionTitle(string id, bool japanese = false)
    {
        var raw = id.Contains('.') ? id[(id.IndexOf('.') + 1)..] : id;
        if (_potionDb.TryGetValue(id, out var e) || _potionDb.TryGetValue("POTION." + raw, out e))
            return japanese ? e.Ja : e.En;
        return ToTitleCase(raw.Replace('_', ' '));
    }

    static string ToTitleCase(string s) =>
        string.Join(' ', s.Split(' ')
            .Select(w => w.Length == 0 ? w : char.ToUpper(w[0]) + w[1..].ToLower()));

    // ---- localization (description / flavor) ----

    record LocData(
        IReadOnlyDictionary<string, string> EngRelics,
        IReadOnlyDictionary<string, string> JpnRelics,
        IReadOnlyDictionary<string, string> EngEvents,
        IReadOnlyDictionary<string, string> JpnEvents);

    static readonly LocData _loc = LoadLoc();

    // カード説明文（バージョン管理: card_descriptions.json）。"CARD.X" → (en, ja)。
    static readonly Dictionary<string, (string En, string Ja)> _cardDesc = LoadCardDescriptions();
    // シナジー判定用に旧 _loc.EngCards と同形（"{rawId}.description" → en）の走査ソースを構築。
    static readonly IReadOnlyDictionary<string, string> _engCardDesc = BuildEngCardDesc();
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
    static readonly HashSet<string> _exhaustAction  = ComputeExhaustAction();
    static readonly HashSet<string> _etherealRelated = ComputeByTag("[gold]Ethereal[/gold]");
    static readonly HashSet<string> _cardExhaustKeyword  = LoadKeywordSet("EXHAUST");
    static readonly HashSet<string> _cardEtherealKeyword = LoadKeywordSet("ETHEREAL");
    // アップグレードで基本と変わるカードのみ収録（rawId → アップグレード後キーワード集合）
    static readonly Dictionary<string, HashSet<string>> _upgradedKeywords = LoadUpgradedKeywords();
    static readonly HashSet<string> _ironcladStrike = ComputeByNameContaining("Strike");
    static readonly HashSet<string> _silentPoison  = ComputeByTag("[gold]Poison[/gold]");
    static readonly HashSet<string> _silentShiv    = ComputeByGoldTagContaining("Shiv");
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
    static readonly HashSet<string> _allEnemiesAttack = ComputeByPlainText("damage to ALL enemies", "hit ALL enemies");

    static HashSet<string> ComputeBlockGivers()
    {
        const string blockTag   = "[gold]Block[/gold]";
        const string channelTag = "[gold]Channel[/gold]";
        const string frostTag   = "[gold]Frost[/gold]";
        const string platingTag = "[gold]Plating[/gold]";
        const string descSuffix = ".description";
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, desc) in _engCardDesc)
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
        foreach (var (key, desc) in _engCardDesc)
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

    static HashSet<string> ComputeExhaustAction()
    {
        const string exhaustTag     = "[gold]Exhaust[/gold]";
        const string exhaustPileTag = "[gold]Exhaust Pile[/gold]";
        const string exhaustedTag   = "[gold]Exhausted[/gold]";
        const string descSuffix     = ".description";
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, desc) in _engCardDesc)
        {
            if (!key.EndsWith(descSuffix, StringComparison.Ordinal)) continue;
            var stripped = desc
                .Replace(exhaustPileTag, "", StringComparison.Ordinal)
                .Replace(exhaustedTag,   "", StringComparison.Ordinal);
            if (stripped.Contains(exhaustTag, StringComparison.Ordinal))
                result.Add(key[..^descSuffix.Length]);
        }
        return result;
    }

    static HashSet<string> ComputeByTag(params string[] tags)
    {
        const string descSuffix = ".description";
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in new[] { _engCardDesc, _loc.EngRelics })
            foreach (var (key, desc) in source)
            {
                if (!key.EndsWith(descSuffix, StringComparison.Ordinal)) continue;
                if (tags.Any(t => desc.Contains(t, StringComparison.Ordinal)))
                    result.Add(key[..^descSuffix.Length]);
            }
        return result;
    }

    // [gold]...[/gold] 内に word が含まれるカード・レリックを収集する。
    // テンプレート形式（例: [gold]{...:plural:Shiv|Shivs}[/gold]）にも対応。
    static HashSet<string> ComputeByGoldTagContaining(string word)
    {
        const string descSuffix = ".description";
        var pattern = new Regex(@"\[gold\][^\[]*" + Regex.Escape(word) + @"[^\[]*\[/gold\]");
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in new[] { _engCardDesc, _loc.EngRelics })
            foreach (var (key, desc) in source)
            {
                if (!key.EndsWith(descSuffix, StringComparison.Ordinal)) continue;
                if (pattern.IsMatch(desc))
                    result.Add(key[..^descSuffix.Length]);
            }
        return result;
    }

    static HashSet<string> ComputeByPlainText(params string[] phrases)
    {
        const string descSuffix = ".description";
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in new[] { _engCardDesc, _loc.EngRelics })
            foreach (var (key, desc) in source)
            {
                if (!key.EndsWith(descSuffix, StringComparison.Ordinal)) continue;
                if (phrases.Any(p => desc.Contains(p, StringComparison.OrdinalIgnoreCase)))
                    result.Add(key[..^descSuffix.Length]);
            }
        return result;
    }

    static HashSet<string> ComputeByNameContaining(string substring)
    {
        // カード名（タイトル）は card_database.json 由来の _db（CARD.* の En）から判定する。
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (id, entry) in _db)
        {
            if (!id.StartsWith("CARD.", StringComparison.OrdinalIgnoreCase)) continue;
            if (entry.En.Contains(substring, StringComparison.OrdinalIgnoreCase))
                result.Add(ToRawId(id));
        }
        return result;
    }

    // Star の増減に「反応」するだけのカード（プレイヤー自身が Star を増減させない）。
    // 例: ブラックホール "Whenever you spend or gain {singleStarIcon}, ..."。
    // 「Starを得る」「Starを使用する」どちらのグループに出しても直観に反するため両方から除外する。
    static HashSet<string> ComputeRegentStarReactive()
        => ComputeByPlainText("spend or gain {singleStarIcon}");

    static HashSet<string> ComputeRegentStarGain()
    {
        var result = ComputeByPlainText("starIcons()", "gain {singleStarIcon}");
        result.Add("ROYAL_GAMBLE"); // "Gain {Stars:diff()} {singleStarIcon}" — 既存パターン非対応のため手動追加
        result.ExceptWith(ComputeRegentStarReactive());
        return result;
    }

    static HashSet<string> ComputeRegentStarSpend()
    {
        var result = ComputeByPlainText(
            "spend {singleStarIcon}",
            "{singleStarIcon} are used", "{singleStarIcon} cost", "{StarThreshold");
        // Starをマナコストとして消費するカードを追加（card_star_costs.json から）
        foreach (var id in LoadStarCostIds())
            result.Add(id);
        result.ExceptWith(ComputeRegentStarReactive());
        return result;
    }

    static IEnumerable<string> LoadStarCostIds()
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = ResourceResolver.ResolveVersioned(asm, "card_star_costs.json");
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

    // card_keywords.json から指定キーワードを持つカードIDセットを構築
    static HashSet<string> LoadKeywordSet(string keyword)
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = ResourceResolver.ResolveVersioned(asm, "card_keywords.json");
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (name is null) return result;
        using var stream = asm.GetManifestResourceStream(name)!;
        var doc = JsonDocument.Parse(stream);
        foreach (var prop in doc.RootElement.EnumerateObject())
            foreach (var kw in prop.Value.EnumerateArray())
                if (kw.GetString()?.Equals(keyword, StringComparison.OrdinalIgnoreCase) == true)
                {
                    result.Add(ToRawId(prop.Name));
                    break;
                }
        return result;
    }

    // card_upgraded_keywords.json から rawId → アップグレード後キーワード集合を構築（無ければ空 dict）
    static Dictionary<string, HashSet<string>> LoadUpgradedKeywords()
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = ResourceResolver.ResolveVersioned(asm, "card_upgraded_keywords.json");
        var result = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        if (name is null) return result;
        using var stream = asm.GetManifestResourceStream(name)!;
        var doc = JsonDocument.Parse(stream);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kw in prop.Value.EnumerateArray())
                if (kw.GetString() is { Length: > 0 } s)
                    set.Add(s);
            result[ToRawId(prop.Name)] = set;
        }
        return result;
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
        foreach (var (key, desc) in _engCardDesc)
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
    public static bool IsExhaustAction(string id)    => _exhaustAction.Contains(ToRawId(id));
    public static bool IsEtherealRelated(string id)  => _etherealRelated.Contains(ToRawId(id));
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
    public static bool IsAllEnemiesAttack(string id) => _allEnemiesAttack.Contains(ToRawId(id));

    static LocData LoadLoc() => new(
        LoadLocJson("eng.relics"),
        LoadLocJson("jpn.relics"),
        LoadLocJson("eng.events"),
        LoadLocJson("jpn.events"));

    static Dictionary<string, (string En, string Ja)> LoadCardDescriptions()
    {
        var asm = Assembly.GetExecutingAssembly();
        var result = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase);
        using var stream = ResourceResolver.OpenText(asm, "card_descriptions.json");
        if (stream is null) return result;

        var doc = JsonDocument.Parse(stream);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            var en = prop.Value.TryGetProperty("en", out var e) ? e.GetString() ?? "" : "";
            var ja = prop.Value.TryGetProperty("ja", out var j) ? j.GetString() ?? "" : en;
            result[prop.Name] = (en, ja);
        }
        return result;
    }

    static IReadOnlyDictionary<string, string> BuildEngCardDesc()
    {
        // "CARD.ABRASIVE" → "ABRASIVE.description"（旧 _loc.EngCards の説明文キー形に合わせる）
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (id, v) in _cardDesc)
            result[ToRawId(id) + ".description"] = v.En;
        return result;
    }

    static IReadOnlyDictionary<string, string> LoadLocJson(string suffix)
    {
        var asm = Assembly.GetExecutingAssembly();
        using var stream = ResourceResolver.OpenText(asm, $"localization.{suffix}.json");
        if (stream is null) return new Dictionary<string, string>();

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

    // "gains [gold]Exhaust[/gold]" を含むエンチャントID（= そのカードに廃棄を付与するエンチャント）
    static readonly HashSet<string> _exhaustGainingEnchantments = ComputeExhaustGainingEnchantments();

    static HashSet<string> ComputeExhaustGainingEnchantments()
    {
        const string suffix  = ".description";
        const string marker  = "[gold]Exhaust[/gold]";
        const string gainStr = "gains ";
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, desc) in _enchantEng)
        {
            if (!key.EndsWith(suffix, StringComparison.Ordinal)) continue;
            int idx = desc.IndexOf(marker, StringComparison.Ordinal);
            if (idx < 0) continue;
            // "gains [gold]Exhaust[/gold]" のみ（"loses" は除外）
            if (idx >= gainStr.Length &&
                desc[(idx - gainStr.Length)..idx].Equals(gainStr, StringComparison.OrdinalIgnoreCase))
                result.Add(key[..^suffix.Length]);
        }
        return result;
    }

    public static bool IsExhaustGainingEnchantment(string enchantmentId) =>
        !string.IsNullOrEmpty(enchantmentId) && _exhaustGainingEnchantments.Contains(ToRawId(enchantmentId));

    // DLL の get_CanonicalKeywords から抽出した正確なキーワード判定
    public static bool IsExhaustKeyword(string id)  => _cardExhaustKeyword.Contains(ToRawId(id));
    public static bool IsEtherealKeyword(string id) => _cardEtherealKeyword.Contains(ToRawId(id));

    // アップグレード状態を考慮したキーワード判定。OnUpgrade で廃棄/幽体が外れる（または付与される）カードは
    // card_upgraded_keywords.json のアップグレード後集合で判定し、変化しないカードは基本判定にフォールバックする。
    public static bool IsExhaustKeyword(string id, bool upgraded) =>
        upgraded && _upgradedKeywords.TryGetValue(ToRawId(id), out var s)
            ? s.Contains("EXHAUST")
            : IsExhaustKeyword(id);
    public static bool IsEtherealKeyword(string id, bool upgraded) =>
        upgraded && _upgradedKeywords.TryGetValue(ToRawId(id), out var s)
            ? s.Contains("ETHEREAL")
            : IsEtherealKeyword(id);

    // ---- card stats (card_stats.json) ----

    static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> _stats = LoadStats();

    static IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> LoadStats()
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = ResourceResolver.ResolveVersioned(asm, "card_stats.json");
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

    // ---- 関連カード (card_related.json: DLL の get_ExtraHoverTips 由来、カードのみ) ----

    static readonly IReadOnlyDictionary<string, string[]> _relatedCards = LoadRelatedCards();
    static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _createdByCards = BuildCreatedBy();

    static IReadOnlyDictionary<string, string[]> LoadRelatedCards()
    {
        var asm = Assembly.GetExecutingAssembly();
        var result = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        var name = ResourceResolver.ResolveVersioned(asm, "card_related.json");
        if (name is null) return result;

        using var stream = asm.GetManifestResourceStream(name)!;
        var doc = JsonDocument.Parse(stream);
        foreach (var prop in doc.RootElement.EnumerateObject())
            result[prop.Name] = prop.Value.EnumerateArray()
                .Select(e => e.GetString() ?? "").Where(s => s.Length > 0).ToArray();
        return result;
    }

    static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildCreatedBy()
    {
        var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (src, targets) in _relatedCards)
            foreach (var t in targets)
            {
                if (!map.TryGetValue(t, out var lst)) map[t] = lst = new List<string>();
                lst.Add(src);
            }
        foreach (var lst in map.Values) lst.Sort(StringComparer.OrdinalIgnoreCase);
        return map.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    static string NormalizeCardId(string id) => id.Contains('.') ? id : "CARD." + id;

    /// <summary>
    /// カードがホバー時に関連表示するカード（DLL の <c>get_ExtraHoverTips</c>、カードのみ）。例: Accuracy → [CARD.SHIV]。
    /// </summary>
    public static IReadOnlyList<string> GetRelatedCards(string id) =>
        _relatedCards.TryGetValue(NormalizeCardId(id), out var r) ? r : Array.Empty<string>();

    /// <summary>
    /// 指定カードを関連表示している（＝そのカードを生成/参照する）カード一覧。<see cref="GetRelatedCards"/> の逆引き。
    /// 例: Shiv ← [CARD.ACCURACY, CARD.BLADE_DANCE, ...]。
    /// </summary>
    public static IReadOnlyList<string> GetCreatedByCards(string id) =>
        _createdByCards.TryGetValue(NormalizeCardId(id), out var r) ? r : Array.Empty<string>();

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
        var name = ResourceResolver.ResolveVersioned(asm, "relic_stats.json");
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

    // ---- event localization ----

    static readonly HashSet<string> _excludedEvents = new(StringComparer.OrdinalIgnoreCase)
        { "DEPRECATED_EVENT", "ERROR", "MOCK_EVENT_MODEL", "PROCEED" };

    public static IEnumerable<string> GetAllEventIds()
    {
        const string titleSuffix = ".title";
        return _loc.EngEvents.Keys
            .Where(k => k.EndsWith(titleSuffix, StringComparison.Ordinal))
            .Select(k => k[..^titleSuffix.Length])
            .Where(id => !id.Contains('.') && !_excludedEvents.Contains(id))
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase);
    }

    public static string GetEventTitle(string id, bool japanese = false)
    {
        var loc = japanese ? _loc.JpnEvents : _loc.EngEvents;
        // relics 同様、ローカライズのキーは接頭辞なし。"EVENT.NEOW" 等の接頭辞付き ID も引けるよう両対応。
        var raw = ToRawId(id);
        if (loc.TryGetValue(id + ".title", out var title) && !string.IsNullOrWhiteSpace(title))
            return title;
        if (loc.TryGetValue(raw + ".title", out var rawTitle) && !string.IsNullOrWhiteSpace(rawTitle))
            return rawTitle;
        return ToTitleCase(raw.Replace('_', ' '));
    }

    public static (string En, string Ja) GetEventDescription(string id)
    {
        var key = $"{id}.pages.INITIAL.description";
        var en  = _loc.EngEvents.TryGetValue(key, out var ev) ? ev : "";
        var ja  = _loc.JpnEvents.TryGetValue(key, out var jv) ? jv : en;
        return (en, ja);
    }

    public static IReadOnlyList<EventOption> GetEventOptions(string id)
    {
        var prefix = $"{id}.pages.INITIAL.options.";
        var optIds = _loc.EngEvents.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                     && k.EndsWith(".title", StringComparison.Ordinal))
            .Select(k => k[prefix.Length..^".title".Length])
            .OrderBy(o => o, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return optIds.Select(optId =>
        {
            var titleKey = $"{prefix}{optId}.title";
            var descKey  = $"{prefix}{optId}.description";
            var titleEn  = _loc.EngEvents.TryGetValue(titleKey, out var te) ? te : "";
            var titleJa  = _loc.JpnEvents.TryGetValue(titleKey, out var tj) ? tj : titleEn;
            var descEn   = _loc.EngEvents.TryGetValue(descKey,  out var de) ? de : "";
            var descJa   = _loc.JpnEvents.TryGetValue(descKey,  out var dj) ? dj : descEn;
            return new EventOption(titleEn, titleJa, descEn, descJa);
        }).ToList();
    }

    public static (string En, string Ja) GetDescription(string id)
    {
        bool isRelic = id.StartsWith("RELIC.", StringComparison.OrdinalIgnoreCase);
        if (!isRelic)
        {
            // カード説明文はバージョン管理の card_descriptions.json から
            return _cardDesc.TryGetValue("CARD." + ToRawId(id), out var d) ? (d.En, d.Ja) : ("", "");
        }
        var key = ToRawId(id) + ".description";
        var en = _loc.EngRelics.TryGetValue(key, out var ev) ? ev : "";
        var ja = _loc.JpnRelics.TryGetValue(key, out var jv) ? jv : en;
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

public record EventOption(string TitleEn, string TitleJa, string DescEn, string DescJa);
