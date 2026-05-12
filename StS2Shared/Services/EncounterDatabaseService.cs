using System.Reflection;
using System.Text.Json;

namespace StS2Shared.Services;

public static class EncounterDatabaseService
{
    static readonly IReadOnlyDictionary<string, string> _engEncounters = LoadJson("eng.encounters");
    static readonly IReadOnlyDictionary<string, string> _jpnEncounters = LoadJson("jpn.encounters");
    static readonly IReadOnlyDictionary<string, string> _engActs       = LoadJson("eng.acts");
    static readonly IReadOnlyDictionary<string, string> _jpnActs       = LoadJson("jpn.acts");

    static IReadOnlyDictionary<string, string> LoadJson(string suffix)
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith($"localization.{suffix}.json", StringComparison.OrdinalIgnoreCase));
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

    static string ToTitleCase(string s) =>
        string.Join(' ', s.Split('_')
            .Select(w => w.Length == 0 ? w : char.ToUpper(w[0]) + w[1..].ToLower()));

    public static string GetEncounterName(string id, bool japanese = false)
    {
        var raw = ToRawId(id);
        var dict = japanese ? _jpnEncounters : _engEncounters;
        return dict.TryGetValue($"{raw}.title", out var v) ? v : ToTitleCase(raw);
    }

    public static string GetActName(string id, bool japanese = false)
    {
        var raw = ToRawId(id);
        var dict = japanese ? _jpnActs : _engActs;
        return dict.TryGetValue($"{raw}.title", out var v) ? v : ToTitleCase(raw);
    }
}
