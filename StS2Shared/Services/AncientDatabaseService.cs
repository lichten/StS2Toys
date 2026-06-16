using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace StS2Shared.Services;

public record TalkLine(int Visit, int Line, bool IsRandom, string Speaker, string Text);
public record TalkCharGroup(string Char, IReadOnlyList<TalkLine> Lines);

public static class AncientDatabaseService
{
    static readonly string[] _charOrder =
        ["firstVisitEver", "ANY", "DEFECT", "IRONCLAD", "NECROBINDER", "REGENT", "SILENT"];

    static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _eng;
    static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _jpn;

    static AncientDatabaseService()
    {
        _eng = LoadAndGroup("eng.ancients");
        _jpn = LoadAndGroup("jpn.ancients");
    }

    static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> LoadAndGroup(string suffix)
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = ResourceResolver.ResolveVersioned(asm, $"localization.{suffix}.json");
        if (name is null) return new Dictionary<string, IReadOnlyDictionary<string, string>>();

        using var stream = asm.GetManifestResourceStream(name)!;
        var doc = JsonDocument.Parse(stream);

        var grouped = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            var key = prop.Name;
            var dot = key.IndexOf('.');
            if (dot < 0) continue;
            var ancientId = key[..dot];
            var subKey = key[(dot + 1)..];
            if (!grouped.TryGetValue(ancientId, out var dict))
                grouped[ancientId] = dict = new(StringComparer.OrdinalIgnoreCase);
            dict[subKey] = prop.Value.GetString() ?? "";
        }

        return grouped.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>(kvp.Value, StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
    }

    static readonly HashSet<string> _excludedIds = new(StringComparer.OrdinalIgnoreCase)
        { "ERROR", "PROCEED" };

    public static IReadOnlyList<string> GetAllAncientIds() =>
        _eng.Keys
            .Where(id => !_excludedIds.Contains(id))
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static string GetAncientTitle(string id, bool japanese = false)
    {
        var dict = japanese ? _jpn : _eng;
        if (dict.TryGetValue(id, out var sub) && sub.TryGetValue("title", out var v) && v != "")
            return v;
        return id;
    }

    public static string GetAncientEpithet(string id, bool japanese = false)
    {
        var dict = japanese ? _jpn : _eng;
        if (dict.TryGetValue(id, out var sub) && sub.TryGetValue("epithet", out var v))
            return v;
        return "";
    }

    public static (string En, string Ja) GetAncientDescription(string id)
    {
        const string key = "pages.INITIAL.description";
        var en = _eng.TryGetValue(id, out var es) && es.TryGetValue(key, out var ev) ? ev : "";
        var ja = _jpn.TryGetValue(id, out var js) && js.TryGetValue(key, out var jv) ? jv : "";
        return (en, ja);
    }

    public static IReadOnlyList<EventOption> GetAncientOptions(string id)
    {
        if (!_eng.TryGetValue(id, out var engSub)) return [];
        _jpn.TryGetValue(id, out var jpnSub);

        const string prefix = "pages.INITIAL.options.";
        var optKeys = engSub.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                     && k.EndsWith(".title", StringComparison.OrdinalIgnoreCase))
            .Select(k => k[prefix.Length..^6])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return optKeys.Select(optKey =>
        {
            var titleKey = $"{prefix}{optKey}.title";
            var descKey  = $"{prefix}{optKey}.description";
            var titleEn = engSub.TryGetValue(titleKey, out var te) ? te : "";
            var titleJa = (jpnSub?.TryGetValue(titleKey, out var tj) == true ? tj : null) ?? titleEn;
            var descEn  = engSub.TryGetValue(descKey,  out var de) ? de : "";
            var descJa  = (jpnSub?.TryGetValue(descKey, out var dj) == true ? dj : null) ?? descEn;
            return new EventOption(titleEn, titleJa, descEn, descJa);
        }).ToList();
    }

    public static IReadOnlyList<TalkCharGroup> GetAncientTalk(string id)
    {
        if (!_eng.TryGetValue(id, out var engSub)) return [];

        var re = new Regex(@"^(\d+)-(\d+)(r?)$");
        var lines = new List<(string Char, int Visit, int Line, bool IsRandom, string Speaker, string Text)>();

        foreach (var (key, text) in engSub)
        {
            // key format: "talk.CHAR.VISIT-LINEr?.SPEAKER"
            var parts = key.Split('.');
            if (parts.Length != 4 || !parts[0].Equals("talk", StringComparison.OrdinalIgnoreCase)) continue;
            var charName = parts[1];
            var speaker  = parts[3];
            var m = re.Match(parts[2]);
            if (!m.Success) continue;
            var visit    = int.Parse(m.Groups[1].Value);
            var line     = int.Parse(m.Groups[2].Value);
            var isRandom = m.Groups[3].Value == "r";
            lines.Add((charName, visit, line, isRandom, speaker, text));
        }

        var orderMap = _charOrder.Select((c, i) => (c, i))
            .ToDictionary(x => x.c, x => x.i, StringComparer.OrdinalIgnoreCase);

        return lines
            .GroupBy(l => l.Char, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => orderMap.TryGetValue(g.Key, out var idx) ? idx : 999)
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new TalkCharGroup(g.Key,
                g.OrderBy(l => l.Visit).ThenBy(l => l.Line)
                 .Select(l => new TalkLine(l.Visit, l.Line, l.IsRandom, l.Speaker, l.Text))
                 .ToList()))
            .ToList();
    }
}
