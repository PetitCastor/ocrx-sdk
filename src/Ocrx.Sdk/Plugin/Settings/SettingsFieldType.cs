namespace Ocrx.Sdk;

/// <summary>The kind of control the engine renders for a projected setting. Mirrors the wire's
/// SettingsFieldType, but plugins never touch generated types — the enum is restated so a plugin
/// can be written against the SDK alone.</summary>
/// <remarks>
/// The wire enum's zero, <c>SETTINGS_FIELD_TYPE_UNSPECIFIED</c>, has no member here: a plugin never
/// declares an unspecified field, exactly as <see cref="RoiKind"/> omits the wire's ROI zero. The
/// mapping in <see cref="SettingsField"/> throws if it ever meets that zero rather than inventing a
/// default the plugin did not ask for.
/// </remarks>
public enum SettingsFieldType
{
    /// <summary>A closed choice from <see cref="SettingsField.Options"/>.</summary>
    Select,

    /// <summary>A boolean; the value is the string "true" or "false".</summary>
    Toggle,

    /// <summary>An integer, bounded by <see cref="SettingsField.Min"/>/<see cref="SettingsField.Max"/>.</summary>
    Int,

    /// <summary>A double, bounded by <see cref="SettingsField.Min"/>/<see cref="SettingsField.Max"/>.</summary>
    Float,

    /// <summary>Free text.</summary>
    String,
}
