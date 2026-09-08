# Compatibility

OCRX 2 is a clean product and package break from everything published before it. There is no
supported source, package, executable, named-pipe, configuration, or installation-path
compatibility layer with any pre-2.0 release.

The protobuf wire contract is intentionally more stable: field numbers, service wire names, and
behavior remain unchanged unless a real protocol change requires otherwise. Generated C# types use
the `Ocrx.Contracts.Proto` namespace.

## Matrix

| Protocol | Engine | SDK train | Plugin train | Notes |
| --- | --- | --- | --- | --- |
| 1 | OCRX 2.0.x | `Ocrx.*` 2.0.x | OCRX plugins 2.0.x | First OCRX release; no compatibility with any pre-2.0 product. |

Package, engine, and plugin versions move independently after 2.0.0. Compatibility is negotiated
by the protocol integer rather than by matching artifact versions.

## Rules

- All packages in this repository share one version and release together.
- SDK patch and minor releases are additive. A major release may require plugin source changes.
- Protocol `Current` changes only for a breaking wire-semantic change, never for additive fields,
  messages, enum values, or RPCs that proto3 peers can ignore safely.
- Protocol `Min` changes only when the engine deliberately drops an older protocol.
- When feasible, an engine supports N-1 for at least one released engine version before raising
  `Min`; security or correctness fixes may require a documented immediate break.
- Plugins should use SDK types rather than generated protobuf or gRPC types.

## Release checklist

1. Publish the matching `Ocrx.Contracts`, `Ocrx.Sdk`, `Ocrx.Sdk.Testing`,
   `Ocrx.Sdk.Overlay`, and `Ocrx.Plugin.Template` packages from `ocrx-sdk`.
2. Build the private engine against the released `Ocrx.Contracts` package and publish the Windows
   binaries and Velopack feed through `ocrx-releases`.
3. Run plugin replay parity against that released engine binary before publishing the plugin.
4. If `ProtocolVersion.Current` or `Min` moved, update this matrix in the same SDK release.
