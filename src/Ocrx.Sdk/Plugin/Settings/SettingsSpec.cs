using ProtoSpec = Ocrx.Contracts.Proto.SettingsSpec;

namespace Ocrx.Sdk;

/// <summary>The full set of settings a plugin projects into the engine's Settings panel, always
/// carrying current values. A plugin sends one on connect and again whenever its settings change;
/// an empty <see cref="Fields"/> list means it has no editable settings and the panel is blank.</summary>
public sealed record SettingsSpec(IReadOnlyList<SettingsField> Fields)
{
    /// <summary>A spec with no fields — the plugin declares no editable settings.</summary>
    public static SettingsSpec Empty { get; } = new(Array.Empty<SettingsField>());

    internal ProtoSpec ToProto()
    {
        var spec = new ProtoSpec();
        foreach (var field in Fields)
        {
            spec.Fields.Add(field.ToProto());
        }

        return spec;
    }

    internal static SettingsSpec FromProto(ProtoSpec spec) =>
        new(spec.Fields.Select(SettingsField.FromProto).ToList());
}
