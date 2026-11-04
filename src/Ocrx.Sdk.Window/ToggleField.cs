namespace Ocrx.Sdk.Window;

/// <summary>A check box.</summary>
public sealed record ToggleField(string Id, string Label, bool Value, string? Description = null)
    : SettingsField(Id, Label, Description);
