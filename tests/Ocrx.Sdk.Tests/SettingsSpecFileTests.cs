using Ocrx.Sdk;

namespace Ocrx.Sdk.Tests;

public sealed class SettingsSpecFileTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("ocrx-settings-spec").FullName;

    [Fact]
    public void WriteThenTryRead_RoundTripsTheFullSchema()
    {
        var spec = new SettingsSpec([new SettingsField("theme", "Theme", SettingsFieldType.Select, "dark", "Choose a theme", [new SettingsOption("dark", "Dark")], Group: "Overlay")]);
        var path = Path.Combine(_directory, SettingsSpecFile.FileName);

        SettingsSpecFile.Write(spec, path);

        Assert.True(SettingsSpecFile.TryRead(path, out var read));
        Assert.Equivalent(spec, read);
    }

    [Fact]
    public void TryRead_MalformedOrMissingFile_ReturnsFalse()
    {
        Assert.False(SettingsSpecFile.TryRead(Path.Combine(_directory, "missing.json"), out _));
        var path = Path.Combine(_directory, SettingsSpecFile.FileName);
        File.WriteAllText(path, "{");
        Assert.False(SettingsSpecFile.TryRead(path, out _));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"fields\":[{\"id\":\"theme\",\"label\":\"Theme\",\"type\":0}]}")]
    public void TryRead_MissingRequiredSchemaValues_ReturnsFalse(string content)
    {
        var path = Path.Combine(_directory, SettingsSpecFile.FileName);
        File.WriteAllText(path, content);

        Assert.False(SettingsSpecFile.TryRead(path, out _));
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
