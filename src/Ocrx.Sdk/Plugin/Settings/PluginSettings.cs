using System.Reflection;

namespace Ocrx.Sdk;

/// <summary>
/// Helpers for the plugin-owned settings lifecycle: seed and load local config, apply validated
/// user edits, persist the plugin config, rebuild local sinks, and publish the effective spec.
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

        var candidate = config.CloneForSettings<TConfig>();
        await validateAndApply(candidate, apply, ct);
        candidate.Save(configPath);
        await services.RebuildOutputsAsync(candidate, ct);
        await services.PublishSettingsAsync(buildSpec(candidate), ct);
        return candidate;
    }
}
