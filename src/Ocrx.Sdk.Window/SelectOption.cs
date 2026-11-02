namespace Ocrx.Sdk.Window;

/// <summary>One choice of a <see cref="SelectField"/>: the value the plugin stores and the text shown.</summary>
public sealed record SelectOption(string Value, string Label);
