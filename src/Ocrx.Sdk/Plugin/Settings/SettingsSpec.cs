namespace Ocrx.Sdk;

/// <summary>
/// The full set of settings a plugin projects into the engine's Settings panel, always carrying
/// current values. An empty <see cref="Fields"/> list means it has no editable settings.
/// </summary>
public sealed record SettingsSpec(IReadOnlyList<SettingsField> Fields)
{
    /// <summary>A spec with no fields - the plugin declares no editable settings.</summary>
    public static SettingsSpec Empty { get; } = new(Array.Empty<SettingsField>());
}
