namespace Ocrx.Sdk.Window;

/// <summary>
/// One edit the user committed. <paramref name="Value"/> is a <see cref="string"/> for select and string
/// fields, a <see cref="bool"/> for toggles, an <see cref="int"/> for int fields and a <see cref="double"/>
/// for float fields.
/// </summary>
public sealed record SettingsEdit(string FieldId, object Value);
