namespace Ocrx.Sdk.Window;

/// <summary>What a plugin answers to a <see cref="SettingsEdit"/>: refreshed fields, or an error.</summary>
public sealed record SettingsResult
{
    private SettingsResult(IReadOnlyList<SettingsField>? fields, string? error)
    {
        Fields = fields;
        Error = error;
    }

    /// <summary>The fields to show now, or null on an error.</summary>
    public IReadOnlyList<SettingsField>? Fields { get; }

    /// <summary>The message to show next to the edited field, or null when the edit was applied.</summary>
    public string? Error { get; }

    /// <summary>The edit was applied and persisted; the form shows these fields, which may differ from before.</summary>
    public static SettingsResult Refresh(IReadOnlyList<SettingsField> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        return new SettingsResult(fields, null);
    }

    /// <summary>The edit was refused; the field goes back to its last good value and shows the message.</summary>
    public static SettingsResult Fail(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return new SettingsResult(null, error);
    }
}
