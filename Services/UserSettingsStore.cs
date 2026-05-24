using System;
using System.IO;
using System.Text.Json;

namespace VictusLounge.Services;

public sealed class UserSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _settingsPath;

    public UserSettingsStore()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _settingsPath = Path.Combine(appData, "VictusLounge", "user-settings.json");
    }

    public UserSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return UserSettings.Default;
            }

            var settings = JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(_settingsPath));
            return settings?.Normalize() ?? UserSettings.Default;
        }
        catch
        {
            return UserSettings.Default;
        }
    }

    public void Save(UserSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(settings.Normalize(), JsonOptions));
        }
        catch
        {
            // User preferences should never block the main application flow.
        }
    }
}

public sealed record UserSettings(
    string Theme,
    string InterfaceSize,
    string Language,
    bool ConfirmClientActions)
{
    public static UserSettings Default { get; } = new("BlackGold", "normal", "ru", true);

    public UserSettings Normalize()
    {
        var theme = Theme is "BlackGold" or "Graphite" or "Light" ? Theme : Default.Theme;
        var size = InterfaceSize is "compact" or "normal" or "large" ? InterfaceSize : Default.InterfaceSize;
        var language = Language is "ru" or "en" ? Language : Default.Language;
        return this with { Theme = theme, InterfaceSize = size, Language = language };
    }
}
