using ProtoOption = Ocrx.Contracts.Proto.SettingsOption;

namespace Ocrx.Sdk;

/// <summary>One choice in a <see cref="SettingsFieldType.Select"/> field: the string that travels
/// the wire when the choice is made, plus the human-facing label the engine shows for it.</summary>
public sealed record SettingsOption(string Value, string Label)
{
    internal ProtoOption ToProto() => new() { Value = Value, Label = Label };

    internal static SettingsOption FromProto(ProtoOption o) => new(o.Value, o.Label);
}
