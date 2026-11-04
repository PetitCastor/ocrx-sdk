namespace Ocrx.Sdk.Window;

/// <summary>A drop-down. <paramref name="Value"/> is the <see cref="SelectOption.Value"/> currently chosen.</summary>
public sealed record SelectField(
    string Id, string Label, string Value, IReadOnlyList<SelectOption> Options, string? Description = null)
    : SettingsField(Id, Label, Description);
