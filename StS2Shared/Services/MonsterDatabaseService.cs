using System.Reflection;
using System.Text.Json;

namespace StS2Shared.Services;

public record MonsterDef(string DirName, string EnLabel, string JaLabel);

public static class MonsterDatabaseService
{
    static readonly IReadOnlyDictionary<string, MonsterDef> _monsters = LoadMonsters();
    static readonly IReadOnlyDictionary<string, string[]>   _encounterMap = LoadEncounterMap();

    static IReadOnlyDictionary<string, MonsterDef> LoadMonsters()
    {
        var asm  = Assembly.GetExecutingAssembly();
        var result = new Dictionary<string, MonsterDef>(StringComparer.OrdinalIgnoreCase);
        var name = ResourceResolver.ResolveVersioned(asm, "monster_names.json");
        if (name is null) return result;
        using var stream = asm.GetManifestResourceStream(name)!;
        var arr = JsonDocument.Parse(stream).RootElement;

        foreach (var el in arr.EnumerateArray())
        {
            var dir = el.GetProperty("dirName").GetString()!;
            var en  = el.GetProperty("en").GetString() ?? dir;
            var ja  = el.GetProperty("ja").GetString() ?? en;
            result[dir] = new MonsterDef(dir, en, ja);
        }
        return result;
    }

    static IReadOnlyDictionary<string, string[]> LoadEncounterMap()
    {
        var asm  = Assembly.GetExecutingAssembly();
        var result = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        var name = ResourceResolver.ResolveVersioned(asm, "encounter_monsters.json");
        if (name is null) return result;
        using var stream = asm.GetManifestResourceStream(name)!;
        var doc = JsonDocument.Parse(stream);

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            var dirs = prop.Value.EnumerateArray().Select(e => e.GetString()!).ToArray();
            result[prop.Name] = dirs;
        }
        return result;
    }

    public static IReadOnlyList<MonsterDef> GetAllMonsters() =>
        _monsters.Values.OrderBy(m => m.EnLabel).ToList();

    public static MonsterDef? GetMonster(string dirName) =>
        _monsters.TryGetValue(dirName, out var m) ? m : null;

    public static MonsterDef GetOrCreate(string dirName)
    {
        if (_monsters.TryGetValue(dirName, out var m)) return m;
        var en = string.Join(' ', dirName.Split('_').Select(w => w.Length == 0 ? w : char.ToUpper(w[0]) + w[1..]));
        return new MonsterDef(dirName, en, en);
    }

    public static bool HasEncounterMapping(string encounterId) =>
        _encounterMap.ContainsKey(encounterId);

    public static string[]? GetEncounterMonsterDirs(string encounterId) =>
        _encounterMap.TryGetValue(encounterId, out var dirs) ? dirs : null;

    public static IReadOnlyList<MonsterDef> GetEncounterMonsters(string encounterId)
    {
        if (!_encounterMap.TryGetValue(encounterId, out var dirs))
            return [];
        return dirs.Select(GetOrCreate).ToList();
    }

    public static IReadOnlyList<string> GetEncounterIdsForMonster(string dirName) =>
        _encounterMap
            .Where(kv => kv.Value.Contains(dirName, StringComparer.OrdinalIgnoreCase))
            .Select(kv => kv.Key)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
