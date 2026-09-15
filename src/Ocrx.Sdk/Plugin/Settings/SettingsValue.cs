namespace Ocrx.Sdk;

/// <summary>
/// One edited value the engine forwards to the plugin. <see cref="Id"/> is opaque to the engine,
/// and <see cref="Value"/> is the new value as a string.
/// </summary>
public sealed record SettingsValue(string Id, string Value);
