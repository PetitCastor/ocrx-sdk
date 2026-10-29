using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;

namespace Ocrx.Sdk;

/// <summary>
/// The contract for a read-only page a plugin shows in a tab of the OCRX window: where the static
/// assets ship, where the data document lives, and how to write and validate it.
/// </summary>
/// <remarks>
/// Both locations are fixed by convention so the engine never reads a path a plugin chose: the assets
/// are <c>view/index.html</c> (plus the files it references) beside the plugin's executable, and the
/// data is <c>%LOCALAPPDATA%\OCRX\{pluginFolder}\view-data.json</c>. There is no channel back from the
/// page to the plugin; the plugin writes, the engine watches and forwards, the page renders.
/// </remarks>
public static class PluginView
{
    /// <summary>Folder, relative to the plugin's install directory, that holds the page's static assets.</summary>
    public const string AssetsFolder = "view";

    /// <summary>Entry document inside <see cref="AssetsFolder"/>. A plugin declares a view by shipping it.</summary>
    public const string EntryFile = "index.html";

    /// <summary>File name of the data document, inside the plugin's data folder.</summary>
    public const string DataFileName = "view-data.json";

    /// <summary>Largest data document, in UTF-8 bytes, that the engine forwards to the page.</summary>
    public const int MaxDataBytes = 1024 * 1024;

    /// <summary>
    /// The conventional data document path, <c>%LOCALAPPDATA%\OCRX\{pluginFolder}\view-data.json</c> —
    /// the same folder <see cref="ConfigSeed.EnsureInLocalAppData"/> uses.
    /// </summary>
    /// <param name="pluginFolder">A single folder name, not a path.</param>
    public static string DataPath(string pluginFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginFolder);
        if (pluginFolder is "." or ".." || pluginFolder.IndexOfAny(['/', '\\', ':']) >= 0
            || pluginFolder.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("The plugin folder must be a single folder name.", nameof(pluginFolder));

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OCRX", pluginFolder, DataFileName);
    }

    /// <summary>
    /// Replaces the data document at <paramref name="path"/> (see <see cref="DataPath"/>) with
    /// <paramref name="data"/>, serialized with the SDK's camel-cased JSON conventions. The write goes
    /// through a temporary file and a rename, so a watcher never observes a half-written document.
    /// </summary>
    /// <exception cref="InvalidOperationException">The document exceeds <see cref="MaxDataBytes"/>.</exception>
    public static void WriteData<T>(string path, T data)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var json = JsonSerializer.Serialize(data, PluginConfig.JsonOptions);
        if (Encoding.UTF8.GetByteCount(json) > MaxDataBytes)
            throw new InvalidOperationException($"View data exceeds {MaxDataBytes} bytes.");

        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        ConfigSeed.Write(full, json);
    }

    /// <summary>
    /// Attempts to read a data document the way the engine does: within <see cref="MaxDataBytes"/> and
    /// valid JSON. Anything else is not a document and is never forwarded.
    /// </summary>
    public static bool TryReadData(string path, [NotNullWhen(true)] out JsonElement? data)
    {
        data = null;
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > MaxDataBytes)
                return false;

            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            data = document.RootElement.Clone();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }
}
