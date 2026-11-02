using System.Drawing;

namespace Ocrx.Sdk.Window.Tests;

public class WindowThemesTests
{
    private static bool Boom() => throw new InvalidOperationException("Light and Dark must not read Windows.");

    [Fact]
    public void IsDark_LightAndDark_DoNotReadWindows()
    {
        Assert.False(WindowThemes.IsDark(WindowTheme.Light, Boom));
        Assert.True(WindowThemes.IsDark(WindowTheme.Dark, Boom));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsDark_System_FollowsWindows(bool windowsDark)
        => Assert.Equal(windowsDark, WindowThemes.IsDark(WindowTheme.System, () => windowsDark));

    [Theory]
    [InlineData(WindowTheme.System, "system")]
    [InlineData(WindowTheme.Light, "light")]
    [InlineData(WindowTheme.Dark, "dark")]
    public void FormatAndTryParse_RoundTrip(WindowTheme theme, string text)
    {
        Assert.Equal(text, WindowThemes.Format(theme));
        Assert.True(WindowThemes.TryParse(text, out var parsed));
        Assert.Equal(theme, parsed);
    }

    [Theory]
    [InlineData(" DARK ", WindowTheme.Dark)]
    [InlineData("Light", WindowTheme.Light)]
    public void TryParse_IgnoresCaseAndSpaces(string text, WindowTheme expected)
    {
        Assert.True(WindowThemes.TryParse(text, out var parsed));
        Assert.Equal(expected, parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("blue")]
    public void TryParse_Unknown_ReturnsFalse(string? text)
        => Assert.False(WindowThemes.TryParse(text, out _));

    [Fact]
    public void ThemeColors_DarkAndLightDiffer()
    {
        Assert.NotEqual(ThemeColors.Back(true), ThemeColors.Back(false));
        Assert.NotEqual(ThemeColors.Fore(true), ThemeColors.Fore(false));
        Assert.Equal(Color.FromArgb(0x0f, 0x17, 0x2a), ThemeColors.Back(true));
    }
}
