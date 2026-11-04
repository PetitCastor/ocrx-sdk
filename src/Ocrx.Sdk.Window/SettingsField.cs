namespace Ocrx.Sdk.Window;

/// <summary>
/// One entry of a <see cref="SettingsForm"/>. <paramref name="Id"/> names the field in a
/// <see cref="SettingsEdit"/> and must be unique in the form. Only the records in this package are drawn.
/// </summary>
public abstract record SettingsField(string Id, string Label, string? Description = null);
