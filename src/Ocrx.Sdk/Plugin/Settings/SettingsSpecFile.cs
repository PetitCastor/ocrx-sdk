using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Ocrx.Sdk;

/// <summary>Reads and writes the settings schema a plugin ships beside its executable.</summary>
public static class SettingsSpecFile
{
    /// <summary>The file name the engine looks for in an installed plugin directory.</summary>
    public const string FileName = "settings-spec.json";

    /// <summary>Writes <paramref name="spec"/> with the SDK's ordinary camel-cased JSON conventions.</summary>
    public static void Write(SettingsSpec spec, string path)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        ConfigSeed.Write(full, JsonSerializer.Serialize(spec, PluginConfig.JsonOptions));
    }

    /// <summary>Attempts to read a shipped schema. Missing or malformed files are not schemas.</summary>
    public static bool TryRead(string path, [NotNullWhen(true)] out SettingsSpec? spec)
    {
        spec = null;
        try
        {
            if (!File.Exists(path))
                return false;

            spec = JsonSerializer.Deserialize<SettingsSpec>(File.ReadAllText(path), PluginConfig.JsonOptions);
            return spec is not null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }
}
