namespace Ocrx.Sdk.Window;

/// <summary>A titled box around other fields. Groups do not nest.</summary>
public sealed record GroupField(string Id, string Label, IReadOnlyList<SettingsField> Fields)
    : SettingsField(Id, Label);
