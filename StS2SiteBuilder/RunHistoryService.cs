using System.Text.Json;

public record RunSummary(
    string FilePath,
    string CharacterId,  // "silent", "ironclad" etc.
    long   StartTime,
    bool   Win,
    bool   WasAbandoned,
    int    Ascension,
    int    RunTime,
    int    TotalFloors
);

public static class RunHistoryService
{
    static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static string GetHistoryDir()
    {
        var roaming  = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var baseDir  = Path.Combine(roaming, "SlayTheSpire2", "steam");
        if (!Directory.Exists(baseDir)) return "";
        var steamDir = Directory.EnumerateDirectories(baseDir).FirstOrDefault();
        return steamDir is null ? "" : Path.Combine(steamDir, "profile1", "saves", "history");
    }

    public static List<RunSummary> LoadSummaries(string historyDir)
    {
        var result = new List<RunSummary>();
        foreach (var file in Directory.EnumerateFiles(historyDir, "*.run"))
        {
            try
            {
                using var stream = File.OpenRead(file);
                var run = JsonSerializer.Deserialize<RunHistoryData>(stream, Options);
                if (run is null) continue;

                var charId = run.Players.Count > 0
                    ? StripPrefix(run.Players[0].Character).ToLowerInvariant()
                    : "";
                var floors = run.MapPointHistory.Sum(act => act.Count);
                result.Add(new RunSummary(file, charId, run.StartTime,
                    run.Win, run.WasAbandoned, run.Ascension, run.RunTime, floors));
            }
            catch { /* 壊れたファイルはスキップ */ }
        }
        result.Sort((a, b) => b.StartTime.CompareTo(a.StartTime));
        return result;
    }

    public static RunHistoryData Load(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return JsonSerializer.Deserialize<RunHistoryData>(stream, Options)
            ?? throw new InvalidDataException("ランファイルを読み込めませんでした。");
    }

    // "CHARACTER.SILENT" → "SILENT", "RELIC.FOO" → "FOO"
    public static string StripPrefix(string id) =>
        id.Contains('.') ? id[(id.IndexOf('.') + 1)..] : id;
}
