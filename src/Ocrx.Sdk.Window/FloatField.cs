namespace Ocrx.Sdk.Window;

/// <summary>
/// A decimal number. With both <paramref name="Min"/> and <paramref name="Max"/> it is a slider with a value
/// readout that moves in <paramref name="Step"/>s from <paramref name="Min"/>; otherwise a number box.
/// The range must be a whole number of steps, and a value off that grid is snapped to the nearest step.
/// <paramref name="Step"/> also sets how many decimals are shown.
/// </summary>
public sealed record FloatField(
    string Id, string Label, double Value, double? Min = null, double? Max = null, double Step = 0.1,
    string? Unit = null, string? Description = null)
    : SettingsField(Id, Label, Description);
