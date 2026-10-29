using System.Collections.Concurrent;
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

    private static readonly ConcurrentDictionary<string, object> WriteLocks =
        new(StringComparer.OrdinalIgnoreCase);

    private const int MoveAttempts = 5;
    private const int MoveRetryDelayMs = 20;

    private static readonly string[] ReservedNames =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    ];

    /// <summary>
    /// The conventional data document path, <c>%LOCALAPPDATA%\OCRX\{pluginFolder}\view-data.json</c> —
    /// the same folder <see cref="ConfigSeed.EnsureInLocalAppData"/> uses. The folder name must equal the
    /// plugin's install folder name, which is how the engine finds it.
    /// </summary>
    /// <param name="pluginFolder">
    /// A single folder name, not a path: no separators, no trailing dot or space, not a Windows
    /// reserved device name.
    /// </param>
    public static string DataPath(string pluginFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginFolder);
        if (pluginFolder is "." or ".." || pluginFolder.IndexOfAny(['/', '\\', ':']) >= 0
            || pluginFolder.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || pluginFolder[^1] is '.' or ' '
            || IsReservedDeviceName(pluginFolder))
            throw new ArgumentException("The plugin folder must be a single, valid folder name.", nameof(pluginFolder));

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OCRX", pluginFolder, DataFileName);
    }

    private static bool IsReservedDeviceName(string name)
    {
        // Windows reserves the device name with or without an extension ("CON.txt").
        var dot = name.IndexOf('.');
        var stem = (dot < 0 ? name : name[..dot]).TrimEnd(' ');
        return ReservedNames.Contains(stem, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Replaces the data document at <paramref name="path"/> (see <see cref="DataPath"/>) with
    /// <paramref name="data"/>, serialized with the SDK's camel-cased JSON conventions. The write goes
    /// through a uniquely named temporary file in the same folder and a rename, so a watcher never
    /// observes a half-written document and concurrent writers never share a temporary file.
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

        var temp = $"{full}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temp, json);
            // File.Replace leaves its own ~RF*.TMP backup files behind when two replaces of the same
            // target collide, so writers in this process take turns per path.
            lock (WriteLocks.GetOrAdd(full, static _ => new object()))
                MoveWithRetry(temp, full);
        }
        finally
        {
            // A leftover temporary file matters less than the exception on its way up, so a failed
            // delete is deliberately not allowed to replace it.
            _ = TryDeleteTemp(temp);
        }
    }

    /// <summary>Deletes <paramref name="temp"/>; true when it is gone, false when it could not be removed.</summary>
    private static bool TryDeleteTemp(string temp)
    {
        try
        {
            File.Delete(temp);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    // The engine's watcher (or an antivirus scan) can hold the target open for a moment; a rename
    // over an open file fails with an IOException that clears on its own.
    private static void MoveWithRetry(string temp, string target)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                // ReplaceFile swaps the contents while a reader holds the old file open with delete
                // sharing, which a plain move-over-existing does not always allow.
                if (File.Exists(target))
                    File.Replace(temp, target, destinationBackupFileName: null);
                else
                    File.Move(temp, target);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException && attempt < MoveAttempts)
            {
                Thread.Sleep(MoveRetryDelayMs * attempt);
            }
        }
    }

    /// <summary>
    /// Attempts to read a data document the way the engine does: within <see cref="MaxDataBytes"/> and
    /// valid JSON. Anything else is not a document and is never forwarded. The file is opened shared
    /// for read, write and delete, so a writer replacing it is never blocked by a reader.
    /// </summary>
    public static bool TryReadData(string path, [NotNullWhen(true)] out JsonElement? data)
    {
        data = null;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);

            // One stream, bounded: a file that grows between a length check and a read cannot
            // slip past the cap.
            var buffer = new byte[MaxDataBytes + 1];
            var read = 0;
            int n;
            while (read < buffer.Length && (n = stream.Read(buffer, read, buffer.Length - read)) > 0)
                read += n;
            if (read > MaxDataBytes)
                return false;

            // JsonDocument rejects a UTF-8 byte order mark; editors and PowerShell write one.
            var start = read >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF ? 3 : 0;
            using var document = JsonDocument.Parse(buffer.AsMemory(start, read - start));
            data = document.RootElement.Clone();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }
}
