using ProtoApply = Ocrx.Contracts.Proto.ApplySettings;

namespace Ocrx.Sdk;

/// <summary>The user-edited values the engine forwards down to a plugin. Only changed fields need
/// appear. The plugin validates each <see cref="SettingsValue"/>, applies it, persists its own
/// config, and re-projects a fresh <see cref="SettingsSpec"/> with the new current values.</summary>
public sealed record ApplySettings(IReadOnlyList<SettingsValue> Values)
{
    internal ProtoApply ToProto()
    {
        var apply = new ProtoApply();
        foreach (var value in Values)
        {
            apply.Values.Add(value.ToProto());
        }

        return apply;
    }

    internal static ApplySettings FromProto(ProtoApply apply) =>
        new(apply.Values.Select(SettingsValue.FromProto).ToList());
}
