using System.Text.Json;

namespace SharkeyWinUI.Services;

/// <summary>
/// File-backed key/value settings store for unpackaged apps.
/// Replaces <c>ApplicationData.Current.LocalSettings</c> which is
/// unavailable when <c>WindowsPackageType=None</c>.
/// </summary>
internal sealed class LocalSettingsService
{
    private static readonly string SettingsFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SharkeyWinUI");

    private static readonly string SettingsFile =
        Path.Combine(SettingsFolder, "settings.json");

    private readonly Dictionary<string, object?> _cache;
    private readonly object _lock = new();

    public LocalSettingsService()
    {
        _cache = Load();
    }

    /// <summary>Gets a stored value, or <c>null</c> if the key doesn't exist.</summary>
    public T? Get<T>(string key)
    {
        lock (_lock)
        {
            if (!_cache.TryGetValue(key, out var raw) || raw is null)
                return default;

            // System.Text.Json deserialises numbers as JsonElement
            if (raw is JsonElement element)
                return element.Deserialize<T>();

            if (raw is T typed)
                return typed;

            return default;
        }
    }

    /// <summary>Stores a value and persists to disk.</summary>
    public void Set<T>(string key, T value)
    {
        lock (_lock)
        {
            _cache[key] = value;
            Save();
        }
    }

    /// <summary>Removes a key and persists to disk.</summary>
    public void Remove(string key)
    {
        lock (_lock)
        {
            _cache.Remove(key);
            Save();
        }
    }

    private void Save()
    {
        Directory.CreateDirectory(SettingsFolder);
        var json = JsonSerializer.Serialize(_cache, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsFile, json);
    }

    private static Dictionary<string, object?> Load()
    {
        if (!File.Exists(SettingsFile))
            return new Dictionary<string, object?>();

        try
        {
            var json = File.ReadAllText(SettingsFile);
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(json)
                   ?? new Dictionary<string, object?>();
        }
        catch
        {
            return new Dictionary<string, object?>();
        }
    }
}
