using System.Reflection;
using System.Text.Json;

namespace StS2Shared.Services;

public static class EventActService
{
    public sealed record ActGroup(string Id, string NameJp, string NameEn, IReadOnlySet<string> Events);

    public static IReadOnlyList<ActGroup> Groups { get; } = Load();

    static IReadOnlyList<ActGroup> Load()
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = ResourceResolver.ResolveVersioned(asm, "event_acts.json");
        if (name is null) return [];

        using var stream = asm.GetManifestResourceStream(name)!;
        using var doc = JsonDocument.Parse(stream);

        var result = new List<ActGroup>();
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            var id     = el.GetProperty("id").GetString() ?? "";
            var nameJp = el.GetProperty("nameJp").GetString() ?? id;
            var nameEn = el.GetProperty("nameEn").GetString() ?? id;
            var events = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var ev in el.GetProperty("events").EnumerateArray())
            {
                var s = ev.GetString();
                if (!string.IsNullOrEmpty(s)) events.Add(s);
            }
            result.Add(new ActGroup(id, nameJp, nameEn, events));
        }
        return result;
    }
}
