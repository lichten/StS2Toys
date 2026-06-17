using System.Reflection;
using System.Text.Json;

namespace StS2Shared.Services;

/// <summary>
/// アクト→戦闘エンカウンターの階層別プール（Weak/Normal/Elite/Boss）＋ボス出現順。
/// card-type-extractor が各 Act クラスの <c>GenerateAllEncounters()</c> /
/// <c>get_BossDiscoveryOrder</c> から生成した encounter_acts.json（バージョンフォルダ）を読む。
/// <see cref="EventActService"/> のエンカウンター版（spiracle Timeline タブ相当）。
/// </summary>
public static class EncounterActService
{
    public sealed record ActEncounters(
        string Id, string NameJp, string NameEn,
        IReadOnlyList<string> Weak, IReadOnlyList<string> Normal,
        IReadOnlyList<string> Elite, IReadOnlyList<string> Boss,
        IReadOnlyList<string> BossOrder);

    public static IReadOnlyList<ActEncounters> Groups { get; } = Load();

    static IReadOnlyList<ActEncounters> Load()
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = ResourceResolver.ResolveVersioned(asm, "encounter_acts.json");
        if (name is null) return [];

        using var stream = asm.GetManifestResourceStream(name)!;
        using var doc = JsonDocument.Parse(stream);

        static IReadOnlyList<string> Arr(JsonElement el, string prop)
        {
            if (!el.TryGetProperty(prop, out var a) || a.ValueKind != JsonValueKind.Array)
                return [];
            var list = new List<string>();
            foreach (var x in a.EnumerateArray())
            {
                var s = x.GetString();
                if (!string.IsNullOrEmpty(s)) list.Add(s);
            }
            return list;
        }

        var result = new List<ActEncounters>();
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            var id     = el.GetProperty("id").GetString() ?? "";
            var nameJp = el.TryGetProperty("nameJp", out var jp) ? jp.GetString() ?? id : id;
            var nameEn = el.TryGetProperty("nameEn", out var en) ? en.GetString() ?? id : id;
            result.Add(new ActEncounters(id, nameJp, nameEn,
                Arr(el, "weak"), Arr(el, "normal"), Arr(el, "elite"),
                Arr(el, "boss"), Arr(el, "bossOrder")));
        }
        return result;
    }
}
