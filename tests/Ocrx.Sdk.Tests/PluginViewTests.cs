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
    public void DataPath_RefusesAnythingButASingleFolderName(string folder)
        => Assert.ThrowsAny<ArgumentException>(() => PluginView.DataPath(folder));

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
