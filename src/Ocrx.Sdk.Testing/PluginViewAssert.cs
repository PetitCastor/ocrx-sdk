namespace Ocrx.Sdk.Testing;

/// <summary>Assertions plugin test suites use to keep a shipped view declaration honest.</summary>
public static class PluginViewAssert
{
    private static readonly string[] ExternalMarkers =
        ["http:", "https:", "=\"//", "='//", "url(//", "from \"//", "from '//"];

    /// <summary>
    /// Throws unless <paramref name="installDirectory"/> ships <c>view/index.html</c> and every
    /// <c>.html</c>, <c>.js</c>, <c>.mjs</c> and <c>.css</c> file under <c>view/</c> avoids absolute
    /// or protocol-relative URLs, which the page's CSP blocks.
    /// </summary>
    public static void IsDeclaredAndSelfContained(string installDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installDirectory);

        var root = Path.Combine(installDirectory, PluginView.AssetsFolder);
        if (!File.Exists(Path.Combine(root, PluginView.EntryFile)))
            throw new InvalidOperationException($"No '{PluginView.AssetsFolder}/{PluginView.EntryFile}' in '{installDirectory}'.");

        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var extension = Path.GetExtension(file).ToLowerInvariant();
            if (extension is not (".html" or ".js" or ".mjs" or ".css"))
                continue;

            var text = File.ReadAllText(file);
            foreach (var marker in ExternalMarkers)
            {
                if (text.Contains(marker, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        $"View asset '{Path.GetRelativePath(installDirectory, file)}' references '{marker}'; the page may load only its own files.");
            }
        }
    }
}
