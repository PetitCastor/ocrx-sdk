using ProtoField = Ocrx.Contracts.Proto.SettingsField;
using ProtoFieldType = Ocrx.Contracts.Proto.SettingsFieldType;

namespace Ocrx.Sdk;

/// <summary>One setting a plugin projects into the engine's Settings panel. The engine renders a
/// generic widget from <see cref="Type"/> and never learns what the field means; the plugin owns
/// all parsing and validation of <see cref="Value"/>, which always travels as a string.</summary>
/// <param name="Id">The plugin's own key for the field, opaque to the engine. The engine echoes it
/// back verbatim in the <see cref="SettingsValue"/> of an edit, so the plugin can route the change.</param>
/// <param name="Label">The human-facing name the engine shows beside the widget.</param>
/// <param name="Type">Which generic widget the engine renders.</param>
/// <param name="Value">The current value, as a string. A <see cref="SettingsFieldType.Toggle"/> is
/// "true"/"false"; an <see cref="SettingsFieldType.Int"/>/<see cref="SettingsFieldType.Float"/> is the
/// number formatted; a <see cref="SettingsFieldType.Select"/> is one of the <see cref="Options"/> values.</param>
/// <param name="Help">Optional longer description the engine may show as help text.</param>
/// <param name="Options">The closed choice set for a <see cref="SettingsFieldType.Select"/>; ignored
/// for every other type.</param>
/// <param name="Min">Lower bound for an <see cref="SettingsFieldType.Int"/>/<see cref="SettingsFieldType.Float"/>;
/// <c>null</c> means unbounded. Null rather than a sentinel because the wire tracks presence
/// (<c>optional double min</c>), so a genuine bound of 0 is distinct from "no bound".</param>
/// <param name="Max">Upper bound, per <see cref="Min"/>; <c>null</c> means unbounded.</param>
/// <param name="Group">Optional section header the engine may group fields under.</param>
/// <remarks>
/// This is a record, but its collection member <see cref="Options"/> compares by reference, not
/// element-wise, so two structurally-identical fields built from different list instances are not
/// equal. The spec is a full idempotent replacement (like RoiSetUpdate), so a plugin's
/// change-detection should never lean on record equality here — at worst it costs a redundant,
/// harmless re-send; it can never miss a real change.
/// </remarks>
public sealed record SettingsField(
    string Id,
    string Label,
    SettingsFieldType Type,
    string Value,
    string Help = "",
    IReadOnlyList<SettingsOption>? Options = null,
    double? Min = null,
    double? Max = null,
    string Group = "")
{
    internal ProtoField ToProto()
    {
        var field = new ProtoField
        {
            Id = Id,
            Label = Label,
            Help = Help,
            Type = ToProto(Type),
            Value = Value,
            Group = Group,
        };

        // Assign the wire's optional min/max only when the POCO carries a bound: the generated
        // setter sets the has-bit on every assignment, so writing 0 for an unbounded field would
        // tell the engine 0 is a real bound. Leaving it unset keeps HasMin/HasMax false.
        if (Min is double min)
        {
            field.Min = min;
        }

        if (Max is double max)
        {
            field.Max = max;
        }

        if (Options is not null)
        {
            foreach (var option in Options)
            {
                field.Options.Add(option.ToProto());
            }
        }

        return field;
    }

    internal static SettingsField FromProto(ProtoField field) => new(
        field.Id,
        field.Label,
        FromProto(field.Type),
        field.Value,
        field.Help,
        // Normalise no-options back to null (not an empty list) so a null-Options field round-trips
        // to itself.
        field.Options.Count > 0 ? field.Options.Select(SettingsOption.FromProto).ToList() : null,
        field.HasMin ? field.Min : null,
        field.HasMax ? field.Max : null,
        field.Group);

    // Both directions of the type mapping live here so a new SettingsFieldType member cannot be
    // added to one switch and missed in the other — the same reason RoiSubscription keeps its mode
    // mapping in one file.
    private static ProtoFieldType ToProto(SettingsFieldType type) => type switch
    {
        SettingsFieldType.Select => ProtoFieldType.Select,
        SettingsFieldType.Toggle => ProtoFieldType.Toggle,
        SettingsFieldType.Int => ProtoFieldType.Int,
        SettingsFieldType.Float => ProtoFieldType.Float,
        SettingsFieldType.String => ProtoFieldType.String,

        // An unmapped kind would otherwise serialise as the proto3 zero (Unspecified) and the
        // engine would render nothing for a field the plugin meant to show.
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown settings field type."),
    };

    private static SettingsFieldType FromProto(ProtoFieldType type) => type switch
    {
        ProtoFieldType.Select => SettingsFieldType.Select,
        ProtoFieldType.Toggle => SettingsFieldType.Toggle,
        ProtoFieldType.Int => SettingsFieldType.Int,
        ProtoFieldType.Float => SettingsFieldType.Float,
        ProtoFieldType.String => SettingsFieldType.String,

        // Unspecified (the proto3 zero) or any value this SDK predates: there is no honest SDK
        // member to hand back, so refuse rather than guess. See SettingsFieldType's remarks.
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown settings field type on the wire."),
    };
}
