using System.Reflection;
using System.Text.Json;

namespace StS2Shared.Services;

public enum KeywordCategory { CardKeyword, Affliction, Enchantment }

public record KeywordEntry(
    string Id,
    KeywordCategory Category,
    string TitleEn,
    string TitleJa,
    string DescEn,
    string DescJa,
    string ExtraCardTextEn,
    string ExtraCardTextJa);

public static class KeywordDatabaseService
{
    static readonly IReadOnlyList<KeywordEntry> _cardKeywords =
        BuildEntries(LoadJson("eng.card_keywords"), LoadJson("jpn.card_keywords"), KeywordCategory.CardKeyword);
    static readonly IReadOnlyList<KeywordEntry> _afflictions =
        BuildEntries(LoadJson("eng.afflictions"), LoadJson("jpn.afflictions"), KeywordCategory.Affliction);
    static readonly IReadOnlyList<KeywordEntry> _enchantments =
        BuildEntries(LoadJson("eng.enchantments"), LoadJson("jpn.enchantments"), KeywordCategory.Enchantment);

    static IReadOnlyDictionary<string, string> LoadJson(string suffix)
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = ResourceResolver.ResolveVersioned(asm, $"localization.{suffix}.json");
        if (name is null) return new Dictionary<string, string>();

        using var stream = asm.GetManifestResourceStream(name)!;
        var doc = JsonDocument.Parse(stream);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in doc.RootElement.EnumerateObject())
            result[prop.Name] = prop.Value.GetString() ?? "";
        return result;
    }

    static IReadOnlyList<KeywordEntry> BuildEntries(
        IReadOnlyDictionary<string, string> eng,
        IReadOnlyDictionary<string, string> jpn,
        KeywordCategory category)
    {
        return eng.Keys
            .Where(k => k.EndsWith(".title", StringComparison.OrdinalIgnoreCase))
            .Select(k => k[..^6])
            .Where(id =>
            {
                eng.TryGetValue($"{id}.title",       out var t);
                eng.TryGetValue($"{id}.description", out var d);
                return !string.IsNullOrEmpty(t) && !string.IsNullOrEmpty(d);
            })
            .Select(id =>
            {
                eng.TryGetValue($"{id}.title",         out var titleEn);
                eng.TryGetValue($"{id}.description",   out var descEn);
                eng.TryGetValue($"{id}.extraCardText",  out var extraEn);
                jpn.TryGetValue($"{id}.title",         out var titleJa);
                jpn.TryGetValue($"{id}.description",   out var descJa);
                jpn.TryGetValue($"{id}.extraCardText",  out var extraJa);
                return new KeywordEntry(id, category,
                    titleEn ?? "", titleJa ?? titleEn ?? "",
                    descEn  ?? "", descJa  ?? descEn  ?? "",
                    extraEn ?? "", extraJa ?? extraEn ?? "");
            })
            .OrderBy(e => e.TitleJa, StringComparer.CurrentCulture)
            .ToList();
    }

    public static IReadOnlyList<KeywordEntry> GetCardKeywords() => _cardKeywords;
    public static IReadOnlyList<KeywordEntry> GetAfflictions()  => _afflictions;
    public static IReadOnlyList<KeywordEntry> GetEnchantments() => _enchantments;
    public static IReadOnlyList<KeywordEntry> GetAll() =>
        [.._cardKeywords, .._afflictions, .._enchantments];
}
