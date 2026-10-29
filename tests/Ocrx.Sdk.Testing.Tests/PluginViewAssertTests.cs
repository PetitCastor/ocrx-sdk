using Ocrx.Sdk.Testing;

namespace Ocrx.Sdk.Testing.Tests;

public sealed class PluginViewAssertTests : IDisposable
{
    private readonly string _install = Directory.CreateTempSubdirectory("ocrx-view-assert").FullName;

    [Fact]
    public void SelfContainedView_Passes()
    {
        Write("index.html", "<script type=\"module\" src=\"app.js\"></script>");
        Write("app.js", "window.addEventListener('message', e => render(e.data));");

        PluginViewAssert.IsDeclaredAndSelfContained(_install);
    }

    [Fact]
    public void MissingEntry_Throws()
        => Assert.Throws<InvalidOperationException>(() => PluginViewAssert.IsDeclaredAndSelfContained(_install));

    [Theory]
    [InlineData("<img src=\"https://example.com/a.png\">")]
    [InlineData("<script src=\"//cdn.example.com/x.js\"></script>")]
    public void ExternalReference_Throws(string html)
    {
        Write("index.html", html);

        Assert.Throws<InvalidOperationException>(() => PluginViewAssert.IsDeclaredAndSelfContained(_install));
    }

    private void Write(string name, string content)
    {
        var dir = Path.Combine(_install, "view");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, name), content);
    }

    public void Dispose() => Directory.Delete(_install, recursive: true);
}
