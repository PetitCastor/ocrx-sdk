using System.Text.RegularExpressions;

namespace Ocrx.Sdk.Testing;

/// <summary>Assertions plugin test suites use to keep a shipped view declaration honest.</summary>
public static partial class PluginViewAssert
{
    private static readonly string[] ScannedExtensions = [".html", ".htm", ".svg", ".css", ".js", ".mjs", ".json"];

    // Anything with a scheme (http:, https:, ws:, wss:, ftp:, data:, blob:, javascript:) or that is
    // protocol-relative (//host) leaves the page's own files. Relative paths pass.
    [GeneratedRegex(@"^(?:[a-z][a-z0-9+.-]*:|//)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExternalReference();

    // Each pattern captures reference targets in group "u". xmlns and createElementNS namespace
    // URIs are identifiers rather than loads, and none of these sites match them.
    private static readonly Regex[] ReferenceSites =
    [
        // HTML / SVG attributes that load or navigate.
        new(@"\b(?:src|href|action|formaction|poster|data|ping|manifest)\s*=\s*(?:""(?<u>[^""]*)""|'(?<u>[^']*)'|(?<u>[^\s>""']+))",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        // CSS url(...), quoted or not.
        new(@"\burl\(\s*(?:""(?<u>[^""]*)""|'(?<u>[^']*)'|(?<u>[^)\s""']*))\s*\)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        // CSS @import "x" (the url(...) form is covered above).
        new(@"@import\s+(?:""(?<u>[^""]*)""|'(?<u>[^']*)')", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        // ES static and dynamic import, re-export.
        new(@"\bimport\s*\(?\s*(?:""(?<u>[^""]*)""|'(?<u>[^']*)'|`(?<u>[^`]*)`)", RegexOptions.CultureInvariant),
        new(@"\bfrom\s*(?:""(?<u>[^""]*)""|'(?<u>[^']*)')", RegexOptions.CultureInvariant),
        // Network and worker constructors and calls.
        new(@"\b(?:fetch|importScripts)\s*\(\s*(?:""(?<u>[^""]*)""|'(?<u>[^']*)'|`(?<u>[^`]*)`)", RegexOptions.CultureInvariant),
        new(@"\bnew\s+(?:WebSocket|EventSource|Worker|SharedWorker)\s*\(\s*(?:""(?<u>[^""]*)""|'(?<u>[^']*)'|`(?<u>[^`]*)`)",
            RegexOptions.CultureInvariant),
        new(@"\.open\s*\(\s*(?:""[A-Za-z]+""|'[A-Za-z]+')\s*,\s*(?:""(?<u>[^""]*)""|'(?<u>[^']*)'|`(?<u>[^`]*)`)",
            RegexOptions.CultureInvariant),
        new(@"\bsendBeacon\s*\(\s*(?:""(?<u>[^""]*)""|'(?<u>[^']*)'|`(?<u>[^`]*)`)", RegexOptions.CultureInvariant),
    ];

    private static readonly Regex SrcSet = new(@"\bsrcset\s*=\s*(?:""(?<u>[^""]*)""|'(?<u>[^']*)')",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex Comments = new(
        @"<!--.*?-->|/\*.*?\*/|(?:^|(?<=[;{},)]))[ \t]*//[^\n]*",
        RegexOptions.Singleline | RegexOptions.Multiline | RegexOptions.CultureInvariant);

    /// <summary>
    /// Throws unless <paramref name="installDirectory"/> ships <c>view/index.html</c> and no
    /// <c>.html</c>, <c>.htm</c>, <c>.svg</c>, <c>.css</c>, <c>.js</c>, <c>.mjs</c> or <c>.json</c> file
    /// under <c>view/</c> loads from outside it. Checked at the places a page actually loads from:
    /// <c>src</c>, <c>href</c>, <c>action</c>, <c>srcset</c>, CSS <c>url()</c> and <c>@import</c>,
    /// ES <c>import</c>, and <c>fetch</c> / <c>XMLHttpRequest</c> / <c>WebSocket</c> / <c>EventSource</c>.
    /// A scheme (<c>http:</c>, <c>https:</c>, <c>ws:</c>, <c>data:</c>, <c>blob:</c>) or <c>//host</c>
    /// target fails; namespace URIs (<c>xmlns</c>) and comments are ignored.
    /// </summary>
    public static void IsDeclaredAndSelfContained(string installDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installDirectory);

        var root = Path.Combine(installDirectory, PluginView.AssetsFolder);
        if (!File.Exists(Path.Combine(root, PluginView.EntryFile)))
            throw new InvalidOperationException($"No '{PluginView.AssetsFolder}/{PluginView.EntryFile}' in '{installDirectory}'.");

        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            if (!ScannedExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                continue;

            var text = Comments.Replace(File.ReadAllText(file), " ");
            foreach (var target in Targets(text))
            {
                if (ExternalReference().IsMatch(target))
                    throw new InvalidOperationException(
                        $"View asset '{Path.GetRelativePath(installDirectory, file)}' loads '{target}'; the page may load only its own files.");
            }
        }
    }

    private static IEnumerable<string> Targets(string text)
    {
        foreach (var site in ReferenceSites)
        {
            foreach (Match match in site.Matches(text))
                yield return match.Groups["u"].Value.Trim();
        }

        foreach (Match match in SrcSet.Matches(text))
        {
            foreach (var candidate in match.Groups["u"].Value.Split(','))
            {
                var url = candidate.Trim().Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
                if (url.Length > 0)
                    yield return url[0];
            }
        }
    }
}
