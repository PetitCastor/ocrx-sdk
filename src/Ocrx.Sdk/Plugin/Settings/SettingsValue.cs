using ProtoValue = Ocrx.Contracts.Proto.SettingsValue;

namespace Ocrx.Sdk;

/// <summary>One edited value the engine forwards to the plugin: <see cref="Id"/> matches a
/// <see cref="SettingsField.Id"/> the plugin declared, and <see cref="Value"/> is the new value as
/// a string, which the plugin parses and validates.</summary>
public sealed record SettingsValue(string Id, string Value)
{
    internal ProtoValue ToProto() => new() { Id = Id, Value = Value };

    internal static SettingsValue FromProto(ProtoValue value) => new(value.Id, value.Value);
}
