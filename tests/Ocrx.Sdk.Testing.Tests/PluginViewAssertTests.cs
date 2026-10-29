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
    [InlineData("index.html", "<img src=\"https://example.com/a.png\">")]
    [InlineData("index.html", "<script src=\"//cdn.example.com/x.js\"></script>")]
    [InlineData("index.html", "<a href='ftp://host/f'>f</a>")]
    [InlineData("index.html", "<form action=wss://host/x></form>")]
    [InlineData("index.html", "<img srcset=\"a.png 1x, https://x/b.png 2x\">")]
    [InlineData("index.html", "<img src=\"data:image/png;base64,AAAA\">")]
    [InlineData("index.html", "<style>body { background: url(\"//x/a.png\") }</style>")]
    [InlineData("app.css", "body { background: url(https://x/a.png) }")]
    [InlineData("app.css", "@import \"https://x/a.css\";")]
    [InlineData("app.css", "@import url('//x/a.css');")]
    [InlineData("app.js", "const m = await import(\"https://x/m.js\");")]
    [InlineData("app.js", "import { a } from 'https://x/m.js';")]
    [InlineData("app.js", "fetch('http://x/api');")]
    [InlineData("app.js", "const r = new XMLHttpRequest(); r.open('GET', 'https://x/api');")]
    [InlineData("app.js", "new WebSocket('wss://x/s');")]
    [InlineData("app.js", "new EventSource('https://x/e');")]
    [InlineData("app.js", "const u = URL.createObjectURL(b); fetch('blob:abc');")]
    [InlineData("pic.svg", "<svg><image href=\"https://x/a.png\"/></svg>")]
    public void ExternalReference_Throws(string file, string content)
    {
        Write("index.html", "<script type=\"module\" src=\"app.js\"></script>");
        Write(file, content);

        Assert.Throws<InvalidOperationException>(() => PluginViewAssert.IsDeclaredAndSelfContained(_install));
    }

    [Theory]
    [InlineData("pic.svg", "<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\"><use href=\"#a\"/></svg>")]
    [InlineData("app.js", "const el = document.createElementNS('http://www.w3.org/2000/svg', 'svg');")]
    [InlineData("app.js", "// see https://example.com/docs\nfetch('./data.json');")]
    [InlineData("app.js", "/* import 'https://x/m.js' */ import './local.js';")]
    [InlineData("app.css", "/* url(https://x/a.png) */ body { background: url(./a.png) }")]
    [InlineData("index.html", "<!-- <script src=\"https://x/a.js\"></script> --><img srcset=\"a.png 1x, b.png 2x\">")]
    [InlineData("app.js", "export { a } from './a.js'; new Worker('./w.js');")]
    public void AllowedReferences_Pass(string file, string content)
    {
        Write("index.html", "<script type=\"module\" src=\"app.js\"></script>");
        Write(file, content);

        PluginViewAssert.IsDeclaredAndSelfContained(_install);
    }

    private void Write(string name, string content)
    {
        var dir = Path.Combine(_install, "view");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, name), content);
    }

    public void Dispose() => Directory.Delete(_install, recursive: true);
}
