# Plugin settings

OCRX plugins can project editable settings into the engine UI without giving the engine ownership
of the plugin's schema or config. The plugin sends a complete `SettingsSpec` upstream on the
existing `Track` stream. The engine renders generic controls from that spec and sends changed
string values back as `ApplySettings` on the same stream.

The raw protobuf compatibility rules remain in [PROTOCOL.md](PROTOCOL.md). This page describes the
SDK-facing contract for plugin authors.

## Stream direction

The plugin is the gRPC client and the engine is the gRPC server.

| Direction | Track arm | Meaning |
| --- | --- | --- |
| Plugin to engine | `SettingsSpec` | Full replacement of the plugin's current editable settings. |
| Engine to plugin | `ApplySettings` | User-edited values for fields the plugin previously declared. |

There is no engine-to-plugin pipe and no generic callback channel. Settings use the existing
bidirectional `Track` stream beside ROI subscriptions and ticks.

## Field model

A `SettingsSpec` contains ordered `SettingsField` entries. Each field is plugin-owned and opaque to
the engine.

| Property | Meaning |
| --- | --- |
| `Id` | Stable plugin key. The engine echoes it back verbatim in `SettingsValue.Id`. |
| `Label` | Human-facing label for the generic editor. |
| `Help` | Optional supporting text for the editor. |
| `Type` | Generic editor kind: `Select`, `Toggle`, `Int`, `Float`, or `String`. |
| `Value` | Current value as a string. All values travel as strings in both directions. |
| `Options` | Ordered choices for `Select`; ignored by other field types. |
| `Min` / `Max` | Optional numeric bounds for `Int` and `Float`; `null` means unbounded. |
| `Group` | Optional group label. The engine may use it to organize fields. |

The engine must not interpret field ids, option values, groups, or value domains. A field id such
as `mode` means nothing to the engine beyond "send this same id back if the user edits it."

## Publishing

Publish a spec when the plugin connects, and publish a fresh complete spec whenever local state
changes. A spec is a full replacement, like an ROI set update, so sending it again is safe.

Override `IOcrxPlugin.OnConnectedAsync` for the initial publish. The host awaits it after the live
Track session is installed and before it starts consuming ticks, so `IPluginServices` is ready to
write the spec:

```csharp
public Task OnConnectedAsync(IPluginServices services, CancellationToken ct) =>
    services.PublishSettingsAsync(BuildSettingsSpec(config), ct);
```

An empty `SettingsSpec` means the plugin has no editable settings. That is also what a new engine
sees from an older plugin that never publishes settings: there is no panel to render, not a failed
connection.

```csharp
await services.PublishSettingsAsync(new SettingsSpec(
[
    new SettingsField(
        "displayDensity",
        "Display density",
        SettingsFieldType.Select,
        config.DisplayDensity,
        Options:
        [
            new SettingsOption("compact", "Compact"),
            new SettingsOption("comfortable", "Comfortable"),
        ],
        Group: "Display"),
    new SettingsField(
        "captureLimit",
        "Capture limit",
        SettingsFieldType.Int,
        config.CaptureLimit.ToString(CultureInfo.InvariantCulture),
        Min: 1,
        Max: 100,
        Group: "Rules"),
]), ct);
```

## Applying changes

`IOcrxPlugin.OnApplySettings` receives an `ApplySettings` batch. Only changed values need appear in
the batch. For each value, the plugin should:

1. Validate that the field id is known.
2. Parse and validate the string value for that field.
3. Apply every value to a candidate config, never the live config.
4. Rebuild the candidate's local sinks and publish its fresh spec, then persist the candidate to
   disk — in that order, so a failed rebuild never leaves the file ahead of the running sinks.
5. Replace the live config only after that complete operation succeeds.

```csharp
public async Task OnApplySettings(
    ApplySettings apply,
    IPluginServices services,
    CancellationToken ct)
{
    config = await PluginSettings.ApplyPersistRebuildAndPublishAsync(
        config,
        configPath,
        apply,
        services,
        (candidate, values, _) =>
        {
            foreach (var value in values.Values)
            {
                switch (value.Id)
                {
                    case "captureLimit":
                        candidate.CaptureLimit = ParseLimit(value.Value);
                        break;
                    case "displayDensity":
                        candidate.DisplayDensity = ParseDensity(value.Value);
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown setting '{value.Id}'.");
                }
            }

            return Task.CompletedTask;
        },
        BuildSettingsSpec,
        ct);
}
```

Throwing from `OnApplySettings` rejects that apply operation for the plugin process and is logged by
the host. It does not stop the plugin run, and it does not change engine ownership: parsing,
validation, persistence, sink rebuilding, and republishing all remain plugin-local.

## Compatibility

Settings are additive. They do not change `ProtocolVersion`.

An older engine ignores the unknown inbound `SettingsSpec` oneof arm. A newer engine connected to
an older plugin simply has no spec to render. Ticks remain separate from settings applies:
`ApplySettings` reaches only `OnApplySettings`, and `TickResult` reaches only the tick path.
