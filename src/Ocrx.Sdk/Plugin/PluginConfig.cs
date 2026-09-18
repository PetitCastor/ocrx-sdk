using System.Text.Json;

namespace Ocrx.Sdk;

/// <summary>
/// The settings every plugin has, and the loader all of them were copying. Plugin-side settings
/// only: everything about *how* the screen is read — monitor, hotkey, OCR language, scan cadence —
/// belongs to the engine's own config, and a plugin that grew those knobs would be describing a
/// capture stack it no longer owns.
/// </summary>
/// <remarks>
/// Derive, add fields, and the loader below writes and reads them without further ceremony. The two
/// existing plugins had the same 20-line <c>Load</c> each, differing only in the type it named.
/// </remarks>
public abstract class PluginConfig
{
    /// <summary>Named pipe the engine listens on; must match the engine's own setting.</summary>
    public string PipeName { get; set; } = EngineDefaults.PipeName;

    /// <summary>
    /// Ask the engine to dump a PNG on every capture and write the plugin's rendering beside it.
    /// The PNG lands in the *engine's* output dir — the plugin only learns the path.
    /// </summary>
    public bool SaveDebugFrames { get; set; }

    /// <summary>
    /// Sinks to build and hand the host, keyed by type — file/HTTP/overlay destinations a plugin
    /// author turns on without writing any sink-wiring code. Empty is today's behaviour: no sinks.
    /// </summary>
    public IReadOnlyList<SinkSpec> Outputs { get; set; } = [];

    /// <summary>
    /// Which generation of the plugin's embedded defaults this file has already been offered.
    /// Bumping it in the embedded default is how <see cref="ConfigSeed"/> is told that a new
    /// default exists; zero means the plugin has not opted in.
    /// </summary>
    /// <remarks>
    /// Only <see cref="ConfigSeed"/> reads it, and only to decide whether to merge. Nothing here
    /// branches on it — this is not a schema version, and an older file is never invalid.
    /// </remarks>
    public int ConfigVersion { get; set; }

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    // Load resolves output paths for runtime use, but settings persistence must retain the relative
    // spelling the user wrote. The slots mirror Outputs by index and carry null for rooted paths.
    private List<string?> _persistedOutputPaths = [];

    /// <summary>
    /// Loads the config, writing a defaults file on first run so the settings are discoverable
    /// without documentation. Same contract as the monolith's ProbeConfig.Load.
    /// </summary>
    /// <remarks>
    /// A config file that exists but deserialises to null — an empty file, or a bare <c>null</c> —
    /// yields defaults rather than throwing, and deliberately does NOT rewrite the file: the user
    /// put something there, and silently replacing it is how a hand-edited config disappears.
    /// </remarks>
    public static T Load<T>(string path) where T : PluginConfig, new()
    {
        if (!File.Exists(path))
        {
            var defaults = new T();
            File.WriteAllText(path, JsonSerializer.Serialize(defaults, JsonOptions));
            NormalizeOutputs(defaults);
            CapturePersistedOutputPaths(defaults);
            defaults.AfterLoad(path);
            ResolveOutputPaths(defaults, path);
            return defaults;
        }

        var config = JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions) ?? new T();
        NormalizeOutputs(config);
        CapturePersistedOutputPaths(config);
        config.AfterLoad(path);
        ResolveOutputPaths(config, path);
        return config;
    }

    /// <summary>
    /// Creates an independent candidate for a settings apply. The candidate retains the loaded
    /// config's relative output-path forms so a successful unrelated edit does not make those paths
    /// absolute when persisted.
    /// </summary>
    public TConfig CloneForSettings<TConfig>(string configPath) where TConfig : PluginConfig
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configPath);

        var json = JsonSerializer.Serialize(this, GetType(), JsonOptions);
        var clone = JsonSerializer.Deserialize(json, GetType(), JsonOptions) as TConfig
            ?? throw new InvalidOperationException($"Could not clone {GetType().FullName}.");

        NormalizeOutputs(clone);
        clone._persistedOutputPaths = [.. _persistedOutputPaths];
        clone.AfterLoad(configPath);
        ResolveOutputPaths(clone, configPath);
        return clone;
    }

    /// <summary>
    /// Writes the config back to <paramref name="path"/> as the same camel-cased, indented JSON
    /// <see cref="Load{T}"/> reads, for a plugin persisting a settings change from
    /// <see cref="IOcrxPlugin.OnApplySettings"/>. Serialised through the runtime type, so a derived
    /// config's own fields are written too.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Writes through a temporary file in the same directory and replaces, so a crash or a full disk
    /// mid-write leaves the previous config intact rather than a truncated one — the same guarantee
    /// <see cref="ConfigSeed"/> makes for the file it seeds.
    /// </para>
    /// <para>
    /// This is a full rewrite of the modelled settings, not a merge: a key the config type does not
    /// bind is not preserved, exactly as <see cref="Load{T}"/> already ignores it.
    /// </para>
    /// </remarks>
    public void Save(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);

        var restored = RestorePersistedRelativePaths(full);
        try
        {
            // The same crash-safe temp-then-replace ConfigSeed uses to protect a file the user edits.
            ConfigSeed.Write(full, JsonSerializer.Serialize(this, GetType(), JsonOptions));
        }
        finally
        {
            foreach (var (spec, resolvedPath) in restored)
                spec.Path = resolvedPath;
        }

        _persistedOutputPaths = Outputs.Select((spec, index) =>
        {
            if (spec is null || string.IsNullOrWhiteSpace(spec.Path))
                return null;

            return index < _persistedOutputPaths.Count && _persistedOutputPaths[index] is { } original
                   && string.Equals(spec.Path, ResolveAgainstConfig(original, full), StringComparison.OrdinalIgnoreCase)
                ? original
                : Path.IsPathRooted(spec.Path) ? null : spec.Path;
        }).ToList();
    }

    /// <summary>
    /// Reconciles output paths after a settings apply. A plugin's apply delegate may set a
    /// <see cref="SinkSpec.Path"/> to a new relative literal (e.g. the user edits a file-sink path);
    /// <see cref="Load{T}"/> and <see cref="CloneForSettings{TConfig}"/> both leave in-memory paths
    /// absolute, so any path still relative here was written by the apply. For each such entry, record
    /// the relative spelling for persistence and resolve the in-memory path to absolute — exactly what
    /// <see cref="Load{T}"/> produces — so the following rebuild resolves against the config directory,
    /// not the process CWD, and <see cref="Save"/> writes the relative form back.
    /// </summary>
    internal void ResolveSettingsOutputPaths(string configPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configPath);

        for (var index = 0; index < Outputs.Count; index++)
        {
            var spec = Outputs[index];
            if (spec is null || string.IsNullOrWhiteSpace(spec.Path) || Path.IsPathRooted(spec.Path))
                continue;

            while (_persistedOutputPaths.Count <= index)
                _persistedOutputPaths.Add(null);
            _persistedOutputPaths[index] = spec.Path;
            spec.Path = ResolveAgainstConfig(spec.Path, configPath);
        }
    }

    /// <summary>
    /// Runs after the values are in place, on both the first-run and the read-back path. Override to
    /// resolve anything that depends on where the config file itself lives — a relative ledger path
    /// against the config's directory, for one. <paramref name="configPath"/> is the file that was
    /// read or written, whether or not it existed a moment ago.
    /// </summary>
    protected virtual void AfterLoad(string configPath) { }

    /// <summary>
    /// Normalizes a null outputs collection to today's empty-output behaviour before path resolution
    /// or sink construction.
    /// </summary>
    private static void NormalizeOutputs(PluginConfig config) => config.Outputs ??= [];

    private static void CapturePersistedOutputPaths(PluginConfig config) =>
        config._persistedOutputPaths = config.Outputs.Select(spec =>
            spec is { Path: { } path } && !Path.IsPathRooted(path) ? path : null).ToList();

    private List<(SinkSpec Spec, string ResolvedPath)> RestorePersistedRelativePaths(string configPath)
    {
        var restored = new List<(SinkSpec Spec, string ResolvedPath)>();
        for (var index = 0; index < Outputs.Count && index < _persistedOutputPaths.Count; index++)
        {
            var spec = Outputs[index];
            var original = _persistedOutputPaths[index];
            if (spec is null || string.IsNullOrWhiteSpace(original) || string.IsNullOrWhiteSpace(spec.Path))
                continue;

            var resolved = ResolveAgainstConfig(original, configPath);
            if (!string.Equals(spec.Path, resolved, StringComparison.OrdinalIgnoreCase))
                continue;

            restored.Add((spec, spec.Path));
            spec.Path = original;
        }

        return restored;
    }

    /// <summary>
    /// Resolves every <see cref="SinkSpec.Path"/> in <see cref="Outputs"/> against the config file's
    /// directory. Run unconditionally by <see cref="Load{T}"/> rather than from <see cref="AfterLoad"/>
    /// itself, because an override like <c>RefineryConfig.AfterLoad</c> does not call the base
    /// implementation.
    /// </summary>
    private static void ResolveOutputPaths(PluginConfig config, string configPath)
    {
        foreach (var spec in config.Outputs)
            if (spec is not null && !string.IsNullOrWhiteSpace(spec.Path))
                spec.Path = ResolveAgainstConfig(spec.Path, configPath);
    }

    /// <summary>
    /// A relative path resolves against the config file's own directory; a rooted path is used
    /// verbatim. The pattern <c>RefineryConfig.ResolveLedgerPath</c> already used for its ledger,
    /// generalised here so every plugin's <see cref="Outputs"/> paths get it for free.
    /// </summary>
    protected static string ResolveAgainstConfig(string path, string configPath)
        => Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(path, Path.GetDirectoryName(Path.GetFullPath(configPath))!);
}
