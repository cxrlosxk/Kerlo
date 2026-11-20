using System.Text.Json;

namespace Kerlo.Services;

public sealed record KerloSettings(int RefreshSeconds = 2, bool AlwaysOnTop = false)
{
    public int SafeRefreshSeconds => RefreshSeconds is 1 or 2 or 5 ? RefreshSeconds : 2;
}

public static class SettingsStore
{
    private static readonly string PathOnDisk = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Kerlo", "settings.json");

    public static KerloSettings Load()
    {
        try
        {
            return File.Exists(PathOnDisk)
                ? JsonSerializer.Deserialize<KerloSettings>(File.ReadAllText(PathOnDisk)) ?? new()
                : new();
        }
        catch { return new(); }
    }

    public static void Save(KerloSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PathOnDisk)!);
            File.WriteAllText(PathOnDisk, JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
        }
        catch { /* Monitoring still works if preferences cannot be written. */ }
    }
}
