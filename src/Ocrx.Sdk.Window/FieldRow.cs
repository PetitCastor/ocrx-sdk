namespace Ocrx.Sdk.Window;

/// <summary>What <see cref="SettingsForm"/> keeps per editable field: how to read, show and revert it.</summary>
internal sealed class FieldRow(Control input, Label error, Func<object> read, Action<object> show, object good)
{
    internal Control Input { get; } = input;

    internal Label Error { get; } = error;

    /// <summary>The control's current value, typed as in <see cref="SettingsEdit"/>.</summary>
    internal Func<object> Read { get; } = read;

    /// <summary>Puts a value in the control without raising an edit.</summary>
    internal Action<object> Show { get; } = show;

    /// <summary>The last value the plugin accepted.</summary>
    internal object Good { get; set; } = good;
}
