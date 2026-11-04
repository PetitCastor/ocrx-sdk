namespace Ocrx.Sdk.Window;

/// <summary>A read-only paragraph.</summary>
public sealed record HelpField(string Id, string Text) : SettingsField(Id, "");
