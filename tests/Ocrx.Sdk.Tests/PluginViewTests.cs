using Ocrx.Sdk;

namespace Ocrx.Sdk.Tests;

public sealed class PluginViewTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("ocrx-plugin-view").FullName;

    [Fact]
    public void WriteData_ThenTryReadData_RoundTrips()
    {
        var path = Path.Combine(_directory, "nested", PluginView.DataFileName);

        PluginView.WriteData(path, new { ItemCount = 3, Title = "Locker" });

        Assert.True(PluginView.TryReadData(path, out var data));
        Assert.Equal(3, data!.Value.GetProperty("itemCount").GetInt32());
        Assert.Equal("Locker", data.Value.GetProperty("title").GetString());
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void WriteData_OverTheCap_ThrowsAndLeavesPreviousDocument()
    {
        var path = Path.Combine(_directory, PluginView.DataFileName);
        PluginView.WriteData(path, new { Ok = true });

        Assert.Throws<InvalidOperationException>(
            () => PluginView.WriteData(path, new string('x', PluginView.MaxDataBytes)));

        Assert.True(PluginView.TryReadData(path, out var data));
        Assert.True(data!.Value.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public void TryReadData_MissingMalformedOrOversized_ReturnsFalse()
    {
        var path = Path.Combine(_directory, PluginView.DataFileName);
        Assert.False(PluginView.TryReadData(path, out _));

        File.WriteAllText(path, "{");
        Assert.False(PluginView.TryReadData(path, out _));

        File.WriteAllText(path, "\"" + new string('x', PluginView.MaxDataBytes) + "\"");
        Assert.False(PluginView.TryReadData(path, out _));
    }

    [Fact]
    public void DataPath_UsesTheConfigSeedFolder()
    {
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OCRX", "ItemTracker", "view-data.json");

        Assert.Equal(expected, PluginView.DataPath("ItemTracker"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("C:evil")]
    [InlineData("plugin.")]
    [InlineData("plugin ")]
    [InlineData("CON")]
    [InlineData("nul")]
    [InlineData("Aux.txt")]
    [InlineData("COM1")]
    [InlineData("lpt9")]
    public void DataPath_RefusesAnythingButASingleFolderName(string folder)
        => Assert.ThrowsAny<ArgumentException>(() => PluginView.DataPath(folder));

    [Fact]
    public void WriteData_ConcurrentWriters_AlwaysLeaveAValidDocumentAndNoTempFiles()
    {
        var path = Path.Combine(_directory, PluginView.DataFileName);

        Parallel.For(0, 40, i => PluginView.WriteData(path, new { Writer = i, Padding = new string('p', 2000) }));

        Assert.True(PluginView.TryReadData(path, out var data));
        Assert.InRange(data!.Value.GetProperty("writer").GetInt32(), 0, 39);
        Assert.Equal([PluginView.DataFileName], Directory.EnumerateFiles(_directory).Select(Path.GetFileName));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \r\n\t ")]
    public void TryReadData_EmptyOrWhitespace_ReturnsFalse(string content)
    {
        var path = Path.Combine(_directory, PluginView.DataFileName);
        File.WriteAllText(path, content);

        Assert.False(PluginView.TryReadData(path, out _));
    }

    [Fact]
    public void TryReadData_Utf8Bom_IsAccepted()
    {
        var path = Path.Combine(_directory, PluginView.DataFileName);
        File.WriteAllText(path, "{\"ok\":true}", new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        Assert.True(PluginView.TryReadData(path, out var data));
        Assert.True(data!.Value.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public void TryReadData_TooDeeplyNested_ReturnsFalse()
    {
        var path = Path.Combine(_directory, PluginView.DataFileName);
        File.WriteAllText(path, new string('[', 200) + new string(']', 200));

        Assert.False(PluginView.TryReadData(path, out _));
    }

    [Fact]
    public void TryReadData_FileHeldOpenByAnotherReader_StillReads()
    {
        var path = Path.Combine(_directory, PluginView.DataFileName);
        PluginView.WriteData(path, new { Ok = true });
        using var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        Assert.True(PluginView.TryReadData(path, out var data));
        Assert.True(data!.Value.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public void WriteData_OverAFileHeldOpenWithDeleteSharing_Replaces()
    {
        var path = Path.Combine(_directory, PluginView.DataFileName);
        PluginView.WriteData(path, new { Version = 1 });
        using var held = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        PluginView.WriteData(path, new { Version = 2 });

        Assert.True(PluginView.TryReadData(path, out var data));
        Assert.Equal(2, data!.Value.GetProperty("version").GetInt32());
    }

    [Fact]
    public void Constants_AreTheDocumentedConvention()
    {
        Assert.Equal("view", PluginView.AssetsFolder);
        Assert.Equal("index.html", PluginView.EntryFile);
        Assert.Equal("view-data.json", PluginView.DataFileName);
        Assert.Equal(1_048_576, PluginView.MaxDataBytes);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
