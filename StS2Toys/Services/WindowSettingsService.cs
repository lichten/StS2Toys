using System.Text.Json;

namespace StS2Toys.Services;

record WindowSettings(int X, int Y, int Width, int Height, string State);

static class WindowSettingsService
{
    static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "StS2Toys", "settings.json");

    static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static WindowSettings? Load()
    {
        if (!File.Exists(SettingsPath)) return null;
        try
        {
            using var stream = File.OpenRead(SettingsPath);
            return JsonSerializer.Deserialize<WindowSettings>(stream, Options);
        }
        catch
        {
            return null;
        }
    }

    public static void Save(WindowSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        using var stream = File.Create(SettingsPath);
        JsonSerializer.Serialize(stream, settings, Options);
    }
}
