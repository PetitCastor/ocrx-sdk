# OCRX SDK

[![CI](https://github.com/PetitCastor/ocrx-sdk/actions/workflows/ci.yml/badge.svg?branch=master)](https://github.com/PetitCastor/ocrx-sdk/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Ocrx.Sdk.svg)](https://www.nuget.org/packages/Ocrx.Sdk)

The public contracts, plugin SDK, testing tools, overlay, template, and protocol source for
[OCRX](https://ocrx.org) — Screen Capture & Optical Processing.

OCRX plugins declare regions of the screen they need and receive structured OCR results from the
private engine over a local named pipe. They do not capture the screen, read game memory, modify
game files, or inspect network traffic.

## Packages

| Package | Purpose |
| --- | --- |
| `Ocrx.Contracts` | Stable gRPC wire contract and shared ROI/OCR value types. |
| `Ocrx.Sdk` | Plugin host, configuration, outputs, and engine client. |
| `Ocrx.Sdk.Testing` | Tick builders, fake services, and real-engine replay harness. |
| `Ocrx.Sdk.Overlay` | Optional click-through Windows overlay output. |
| `Ocrx.Plugin.Template` | `dotnet new ocrx-plugin` project template. |

All OCRX 2 packages use the same version train, and the first public release is `2.0.0`. Nothing
published before it is source-, package-, install-path-, or endpoint-compatible with OCRX 2. Those
pre-2.0 packages are unlisted on nuget.org: an existing pin still restores, but they no longer
appear in search and are not offered to new consumers.

## Create a plugin

```powershell
dotnet new install Ocrx.Plugin.Template --version 2.0.0
dotnet new ocrx-plugin -n MyPlugin
dotnet test MyPlugin/tests/MyPlugin.Tests.csproj
```

The generated plugin targets plain `net10.0`, uses the `OCRX.Engine` named pipe, and pins all OCRX
packages to the same 2.x release. Set `OCRX_ENGINE_PATH` only for replay tests that launch a real
`Ocrx.Engine.exe`.

## Build the SDK

Requires the .NET 10 SDK.

```powershell
dotnet restore OcrxSdk.slnx
dotnet build OcrxSdk.slnx --no-restore
dotnet test OcrxSdk.slnx --no-build --filter "Category!=Integration"
```

The package projects use project references inside this repository so one SDK commit remains an
atomic, reviewable unit. The private engine consumes released `Ocrx.Contracts` packages and does
not reference these projects by path.

## Documentation

- [Plugin authoring](docs/PLUGIN-AUTHORING.md)
- [Plugin settings](docs/PLUGIN-SETTINGS.md)
- [Protocol and compatibility rules](docs/PROTOCOL.md)
- [Replay and parity testing](docs/REPLAY.md)
- [Release compatibility matrix](docs/COMPATIBILITY.md)
- [Public website documentation](https://ocrx.org/docs)

The protobuf package, field numbers, service names, and behavior remain unchanged from the v1 wire
contract; generated C# types live under `Ocrx.Contracts.Proto`.

## Related repositories

- [ocrx-engine](https://github.com/PetitCastor/ocrx-engine) — private engine source after the v2 migration
- [ocrx-plugins](https://github.com/PetitCastor/ocrx-plugins) — public plugins and catalog
- [ocrx-releases](https://github.com/PetitCastor/ocrx-releases) — public Windows binaries and update feed
- [ocrx-site](https://github.com/PetitCastor/ocrx-site) — website and documentation

## License

[MIT](LICENSE)
