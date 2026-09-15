using System.Reflection;

namespace Ocrx.Sdk;

/// <summary>
/// Helpers for the plugin-owned settings lifecycle: seed and load local config, apply validated
/// user edits, rebuild local sinks, publish the effective spec, and only then persist the plugin
/// config — so a failed rebuild leaves neither the live config nor the file half-configured.
/// </summary>
public static class PluginSettings
{
    public static TConfig LoadLocalConfig<TConfig>(
        Assembly assembly,
        string resourceName,
        string pluginFolder,
        string fileName = "config.json")
        where TConfig : PluginConfig, new()
    {
        var path = ConfigSeed.EnsureInLocalAppData(assembly, resourceName, pluginFolder, fileName);
        return PluginConfig.Load<TConfig>(path);
    }

    public static async Task<TConfig> ApplyPersistRebuildAndPublishAsync<TConfig>(
        TConfig config,
        string configPath,
        ApplySettings apply,
        IPluginServices services,
        Func<TConfig, ApplySettings, CancellationToken, Task> validateAndApply,
        Func<TConfig, SettingsSpec> buildSpec,
        CancellationToken ct = default)
        where TConfig : PluginConfig
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(configPath);
        ArgumentNullException.ThrowIfNull(apply);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(validateAndApply);
        ArgumentNullException.ThrowIfNull(buildSpec);

        var candidate = config.CloneForSettings<TConfig>(configPath);
        await validateAndApply(candidate, apply, ct);
        candidate.ResolveSettingsOutputPaths(configPath);

        // Rebuild the live sinks and publish the spec *before* committing the file. A rebuild builds
        // real file/HTTP/overlay sinks from the user-edited config and can throw on bad input; if it
        // does, nothing is persisted, so the on-disk config never runs ahead of the live one and the
        // plugin does not reload a config whose rebuild just failed on the next start.
        await services.RebuildOutputsAsync(candidate, ct);
        await services.PublishSettingsAsync(buildSpec(candidate), ct);
        candidate.Save(configPath);
        return candidate;
    }
}
