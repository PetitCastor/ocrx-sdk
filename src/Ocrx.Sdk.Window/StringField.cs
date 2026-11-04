namespace Ocrx.Sdk.Window;

/// <summary>A text box; an edit is sent when the box loses focus or Enter is pressed.</summary>
public sealed record StringField(string Id, string Label, string Value, string? Description = null)
    : SettingsField(Id, Label, Description);
