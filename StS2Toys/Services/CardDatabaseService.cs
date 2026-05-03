using System.Reflection;
using System.Text.Json;

namespace StS2Toys.Services;

static class CardDatabaseService
{
    record Entry(string En, string Ja);

    static readonly Dictionary<string, Entry> _db = Load();
    static readonly Dictionary<string, string> _types = LoadTypes();

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

    public static string GetCardType(string id)
    {
        _types.TryGetValue(id, out var type);
        return type ?? "";
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
