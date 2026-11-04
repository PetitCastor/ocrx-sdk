namespace Ocrx.Sdk.Window;

/// <summary>
/// A whole number. With both <paramref name="Min"/> and <paramref name="Max"/> it is a slider with a value
/// readout that moves in <paramref name="Step"/>s from <paramref name="Min"/>; otherwise a number box.
/// The range must be a whole number of steps, and a value off that grid is snapped to the nearest step.
/// </summary>
public sealed record IntField(
    string Id, string Label, int Value, int? Min = null, int? Max = null, int Step = 1, string? Unit = null,
    string? Description = null)
    : SettingsField(Id, Label, Description);
