using System.Text.Json;

namespace StS2Toys.Services;

record WindowSettings(int X, int Y, int Width, int Height, string State);
record SubWindowSettings(int X, int Y, int Width, int Height, bool Visible = false);
record AppSettings(
    WindowSettings? Main = null,
    SubWindowSettings? ImageViewer = null,
    SubWindowSettings? CardDetail = null,
    SubWindowSettings? DeckOverview = null,
    SubWindowSettings? BlockOverview = null,
    SubWindowSettings? HpHistory = null,
    SubWindowSettings? DrawOverview = null,
    SubWindowSettings? EncounterOverview = null,
    int? SidePanelWidth = null);

static class WindowSettingsService
{
    static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "StS2Toys", "settings.json");

    static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        if (!File.Exists(SettingsPath)) return new AppSettings();
        try
        {
            using var stream = File.OpenRead(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(stream, Options) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        using var stream = File.Create(SettingsPath);
        JsonSerializer.Serialize(stream, settings, Options);
    }
}
