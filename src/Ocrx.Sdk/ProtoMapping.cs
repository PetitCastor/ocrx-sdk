using Ocrx.Contracts.Proto;
using ProtoApply = Ocrx.Contracts.Proto.ApplySettings;
using ProtoField = Ocrx.Contracts.Proto.SettingsField;
using ProtoFieldType = Ocrx.Contracts.Proto.SettingsFieldType;
using ProtoOption = Ocrx.Contracts.Proto.SettingsOption;
using ProtoSpec = Ocrx.Contracts.Proto.SettingsSpec;
using ProtoValue = Ocrx.Contracts.Proto.SettingsValue;

namespace Ocrx.Sdk;

/// <summary>
/// The SDK-owned boundary between plugin-facing settings models and generated protobuf messages.
/// Plugin authors stay on the plain SDK types; transport code is the only place that calls here.
/// </summary>
internal static class ProtoMapping
{
    internal static ProtoSpec ToProto(this SettingsSpec spec)
    {
        var proto = new ProtoSpec();
        foreach (var field in spec.Fields)
        {
            proto.Fields.Add(field.ToProto());
        }

        return proto;
    }

    internal static SettingsSpec ToSettingsSpec(this ProtoSpec spec) =>
        new(spec.Fields.Select(ToSettingsField).ToList());

    internal static ProtoApply ToProto(this ApplySettings apply)
    {
        var proto = new ProtoApply();
        foreach (var value in apply.Values)
        {
            proto.Values.Add(value.ToProto());
        }

        return proto;
    }

    internal static ApplySettings ToApplySettings(this ProtoApply apply) =>
        new(apply.Values.Select(ToSettingsValue).ToList());

    private static ProtoField ToProto(this SettingsField field)
    {
        var proto = new ProtoField
        {
            Id = field.Id,
            Label = field.Label,
            Help = field.Help,
            Type = field.Type.ToProto(),
            Value = field.Value,
            Group = field.Group,
        };

        if (field.Min is double min)
        {
            proto.Min = min;
        }

        if (field.Max is double max)
        {
            proto.Max = max;
        }

        if (field.Options is not null)
        {
            foreach (var option in field.Options)
            {
                proto.Options.Add(option.ToProto());
            }
        }

        return proto;
    }

    private static SettingsField ToSettingsField(this ProtoField field) => new(
        field.Id,
        field.Label,
        field.Type.ToSettingsFieldType(),
        field.Value,
        field.Help,
        field.Options.Count > 0 ? field.Options.Select(ToSettingsOption).ToList() : null,
        field.HasMin ? field.Min : null,
        field.HasMax ? field.Max : null,
        field.Group);

    private static ProtoOption ToProto(this SettingsOption option) =>
        new() { Value = option.Value, Label = option.Label };

    private static SettingsOption ToSettingsOption(this ProtoOption option) =>
        new(option.Value, option.Label);

    private static ProtoValue ToProto(this SettingsValue value) =>
        new() { Id = value.Id, Value = value.Value };

    private static SettingsValue ToSettingsValue(this ProtoValue value) =>
        new(value.Id, value.Value);

    private static ProtoFieldType ToProto(this SettingsFieldType type) => type switch
    {
        SettingsFieldType.Select => ProtoFieldType.Select,
        SettingsFieldType.Toggle => ProtoFieldType.Toggle,
        SettingsFieldType.Int => ProtoFieldType.Int,
        SettingsFieldType.Float => ProtoFieldType.Float,
        SettingsFieldType.String => ProtoFieldType.String,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown settings field type."),
    };

    private static SettingsFieldType ToSettingsFieldType(this ProtoFieldType type) => type switch
    {
        ProtoFieldType.Select => SettingsFieldType.Select,
        ProtoFieldType.Toggle => SettingsFieldType.Toggle,
        ProtoFieldType.Int => SettingsFieldType.Int,
        ProtoFieldType.Float => SettingsFieldType.Float,
        ProtoFieldType.String => SettingsFieldType.String,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown settings field type on the wire."),
    };
}
