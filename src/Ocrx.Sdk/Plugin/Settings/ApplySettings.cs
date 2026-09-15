namespace Ocrx.Sdk;

/// <summary>
/// The user-edited values the engine forwards down to a plugin. Only changed fields need appear.
/// The plugin validates each value before applying it to its own config.
/// </summary>
public sealed record ApplySettings(IReadOnlyList<SettingsValue> Values);
