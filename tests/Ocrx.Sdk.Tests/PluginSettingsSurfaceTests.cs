using Ocrx.Contracts;
using Xunit;

namespace Ocrx.Sdk.Tests;

/// <summary>
/// The plugin-facing settings surface (PSET-03): the host routes an engine-forwarded
/// <see cref="ApplySettings"/> to the plugin under the same one-failure-survives rule a tick gets,
/// a plugin publishes and rebuilds through <see cref="IPluginServices"/>, the output pipeline can
/// swap its composed sink live, and a config persists and reloads.
/// </summary>
public class PluginSettingsSurfaceTests
{
    private static TickDispatcher Dispatcher(StubPlugin plugin, out RecordingOutput output)
    {
        output = new RecordingOutput();
        var services = new PluginServices([], output, verbose: true, dumpFrame: null);
        return new TickDispatcher(plugin, services, output);
    }

    private static CaptureRecord Record(string text = "SETUP")
        => new(DateTime.UtcNow, "plugin", TriggerKind.Auto, text);

    // ---------- dispatch of a forwarded edit ----------

    [Fact]
    public async Task AnApply_ReachesTheOnApplySettingsHook()
    {
        var plugin = new StubPlugin();
        var dispatcher = Dispatcher(plugin, out _);

        await dispatcher.ApplySettingsAsync(
            new ApplySettings([new SettingsValue("theme", "dark")]), default);

        var apply = Assert.Single(plugin.Applied);
        var value = Assert.Single(apply.Values);
        Assert.Equal("theme", value.Id);
        Assert.Equal("dark", value.Value);
    }

    /// <summary>One bad apply must not end the run, exactly as one bad tick does not.</summary>
    [Fact]
    public async Task WhenTheApplyThrows_TheFailureIsLoggedAndSwallowed()
    {
        var plugin = new StubPlugin(
            onApply: (_, _, _) => throw new InvalidOperationException("validate exploded"))
        {
            Name = "signature",
        };
        var dispatcher = Dispatcher(plugin, out var output);

        await dispatcher.ApplySettingsAsync(new ApplySettings([]), default);

        Assert.Contains("settings apply failed", output.Text);
        Assert.Contains("validate exploded", output.Text);
    }

    /// <summary>A cancellation is the run ending, not a per-apply failure, so it propagates.</summary>
    [Fact]
    public async Task WhenTheApplyCancels_ItPropagates()
    {
        var plugin = new StubPlugin(
            onApply: (_, _, _) => throw new OperationCanceledException());
        var dispatcher = Dispatcher(plugin, out _);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => dispatcher.ApplySettingsAsync(new ApplySettings([]), default));
    }

    // ---------- publish / rebuild through the services ----------

    [Fact]
    public async Task PublishSettings_WithNoLiveSession_IsANoOp()
    {
        var services = new PluginServices([], new RecordingOutput(), verbose: false, dumpFrame: null);

        // No Session set — the ordinary "between connects" state. Must not throw; the plugin
        // re-publishes on the next connect.
        await services.PublishSettingsAsync(SettingsSpec.Empty);
    }

    [Fact]
    public async Task OnConnect_ProvidesLiveServicesForTheInitialSettingsSpec()
    {
        var spec = new SettingsSpec(
            [new SettingsField("theme", "Theme", SettingsFieldType.Select, "light")]);
        SettingsSpec? published = null;
        var plugin = new StubPlugin(onConnected: async (services, ct) =>
            await services.PublishSettingsAsync(spec, ct));
        var output = new RecordingOutput();
        var services = new PluginServices([], output, verbose: false, dumpFrame: null)
        {
            PublishSettingsHandler = (actual, _) =>
            {
                published = actual;
                return Task.CompletedTask;
            },
        };
        var dispatcher = new TickDispatcher(plugin, services, output);

        await dispatcher.OnConnectedAsync(default);

        Assert.Same(spec, published);
    }

    [Fact]
    public async Task RebuildOutputs_InvokesTheHostHandlerWithTheConfig()
    {
        var services = new PluginServices([], new RecordingOutput(), verbose: false, dumpFrame: null);
        PluginConfig? seen = null;
        services.RebuildOutputsHandler = (cfg, _) => { seen = cfg; return Task.CompletedTask; };
        var config = new BareConfig { PipeName = "P" };

        await services.RebuildOutputsAsync(config);

        Assert.Same(config, seen);
    }

    [Fact]
    public async Task RebuildOutputs_WithNoHandler_IsANoOp()
    {
        var services = new PluginServices([], new RecordingOutput(), verbose: false, dumpFrame: null);

        // A test double or embedding host that never wired the seam: the default path does nothing.
        await services.RebuildOutputsAsync(new BareConfig());
    }

    // ---------- live sink swap ----------

    [Fact]
    public async Task ReplaceSink_SwapsLive_DisposingTheOldAndDeliveringToTheNew()
    {
        var output = new RecordingOutput();
        var first = new FakeRecordSink();
        var pipeline = new PluginOutputPipeline(first, output);
        pipeline.Start(CancellationToken.None);

        var second = new FakeRecordSink();
        await pipeline.ReplaceSinkAsync(second);

        Assert.True(first.Disposed);

        var record = Record();
        pipeline.Enqueue(record);
        await pipeline.CompleteAndDrainAsync();

        Assert.Contains(record, second.Received);
        Assert.Empty(first.Received);
        Assert.True(second.Disposed);
    }

    // ---------- config persist ----------

    [Fact]
    public void Save_RoundTripsThroughLoad_IncludingDerivedFields()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ocrx-cfg-{Guid.NewGuid():N}.json");
        try
        {
            new SaveConfig { PipeName = "PipeX", Theme = "midnight" }.Save(path);

            var loaded = PluginConfig.Load<SaveConfig>(path);

            Assert.Equal("PipeX", loaded.PipeName);
            Assert.Equal("midnight", loaded.Theme);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Save_CreatesMissingDirectories()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ocrx-cfg-{Guid.NewGuid():N}");
        var path = Path.Combine(dir, "nested", "config.json");
        try
        {
            new SaveConfig().Save(path);

            Assert.True(File.Exists(path));
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyPersistRebuildAndPublishAsync_PersistsRebuildsAndPublishes()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ocrx-settings-{Guid.NewGuid():N}");
        var path = Path.Combine(dir, "config.json");
        var output = new RecordingOutput();
        var services = new PluginServices([], output, verbose: false, dumpFrame: null);
        PluginConfig? rebuilt = null;
        SettingsSpec? published = null;
        services.RebuildOutputsHandler = (cfg, _) => { rebuilt = cfg; return Task.CompletedTask; };
        services.PublishSettingsHandler = (spec, _) =>
        {
            published = spec;
            return Task.CompletedTask;
        };
        var config = new SaveConfig { Theme = "light" };

        try
        {
            var updated = await PluginSettings.ApplyPersistRebuildAndPublishAsync(
                config,
                path,
                new ApplySettings([new SettingsValue("theme", "dark")]),
                services,
                (cfg, apply, _) =>
                {
                    var value = Assert.Single(apply.Values);
                    Assert.Equal("theme", value.Id);
                    cfg.Theme = value.Value;
                    return Task.CompletedTask;
                },
                cfg => new SettingsSpec([new SettingsField("theme", "Theme", SettingsFieldType.String, cfg.Theme)]));

            var saved = PluginConfig.Load<SaveConfig>(path);
            Assert.Equal("dark", saved.Theme);
            Assert.Equal("light", config.Theme);
            Assert.Equal("dark", updated.Theme);
            Assert.Same(updated, rebuilt);
            Assert.NotNull(published);
            var publishedField = Assert.Single(published!.Fields);
            Assert.Equal("theme", publishedField.Id);
            Assert.Equal("dark", publishedField.Value);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyPersistRebuildAndPublishAsync_WhenValidationFails_LeavesTheLiveConfigUntouched()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ocrx-settings-{Guid.NewGuid():N}.json");
        var services = new PluginServices([], new RecordingOutput(), verbose: false, dumpFrame: null);
        var config = new SaveConfig { Theme = "light" };

        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                PluginSettings.ApplyPersistRebuildAndPublishAsync(
                    config,
                    path,
                    new ApplySettings([new SettingsValue("theme", "dark"), new SettingsValue("other", "x")]),
                    services,
                    (_, _, _) => throw new InvalidOperationException("invalid batch"),
                    cfg => new SettingsSpec([new SettingsField("theme", "Theme", SettingsFieldType.String, cfg.Theme)])));

            Assert.Equal("light", config.Theme);
            Assert.False(File.Exists(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ApplyPersistRebuildAndPublishAsync_PreservesLoadedRelativeOutputPaths()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ocrx-settings-{Guid.NewGuid():N}");
        var path = Path.Combine(dir, "config.json");
        Directory.CreateDirectory(dir);
        var initial = new SaveConfig
        {
            Theme = "light",
            Outputs = [new SinkSpec { Type = "json", Path = "records.jsonl" }],
        };
        initial.Save(path);
        var config = PluginConfig.Load<SaveConfig>(path);
        var services = new PluginServices([], new RecordingOutput(), verbose: false, dumpFrame: null);

        try
        {
            _ = await PluginSettings.ApplyPersistRebuildAndPublishAsync(
                config,
                path,
                new ApplySettings([new SettingsValue("theme", "dark")]),
                services,
                (cfg, apply, _) =>
                {
                    cfg.Theme = Assert.Single(apply.Values).Value;
                    return Task.CompletedTask;
                },
                cfg => new SettingsSpec([new SettingsField("theme", "Theme", SettingsFieldType.String, cfg.Theme)]));

            Assert.Contains("\"path\": \"records.jsonl\"", File.ReadAllText(path));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private sealed class BareConfig : PluginConfig;

    private sealed class SaveConfig : PluginConfig
    {
        public string Theme { get; set; } = "light";
    }
}
