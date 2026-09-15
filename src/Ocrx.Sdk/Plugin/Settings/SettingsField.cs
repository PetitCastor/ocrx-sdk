namespace Ocrx.Sdk;

/// <summary>
/// One setting a plugin projects into the engine's Settings panel. The engine renders a generic
/// widget from <see cref="Type"/> and never learns what the field means; the plugin owns all
/// parsing and validation of <see cref="Value"/>, which always travels as a string.
/// </summary>
public sealed record SettingsField(
    string Id,
    string Label,
    SettingsFieldType Type,
    string Value,
    string Help = "",
    IReadOnlyList<SettingsOption>? Options = null,
    double? Min = null,
    double? Max = null,
    string Group = "");
