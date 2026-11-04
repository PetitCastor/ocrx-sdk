namespace Ocrx.Sdk.Window;

/// <summary>Stores a <see cref="WindowTheme"/> as text and tells whether it is dark right now.</summary>
public static class WindowThemes
{
    private const string SystemValue = "system";
    private const string LightValue = "light";
    private const string DarkValue = "dark";

    /// <summary>The lower-case text form of <paramref name="theme"/>, as <see cref="TryParse"/> reads it.</summary>
    public static string Format(WindowTheme theme) => theme switch
    {
        WindowTheme.Light => LightValue,
        WindowTheme.Dark => DarkValue,
        _ => SystemValue,
    };

    /// <summary>Reads "system", "light" or "dark" in any case; anything else returns false.</summary>
    public static bool TryParse(string? value, out WindowTheme theme)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case SystemValue: theme = WindowTheme.System; return true;
            case LightValue: theme = WindowTheme.Light; return true;
            case DarkValue: theme = WindowTheme.Dark; return true;
            default: theme = WindowTheme.System; return false;
        }
    }

    /// <summary>Whether the theme is dark now. Only <see cref="WindowTheme.System"/> reads Windows.</summary>
    public static bool IsDark(WindowTheme theme) => IsDark(theme, Win32WindowPlatform.ReadAppsAreDark);

    internal static bool IsDark(WindowTheme theme, Func<bool> systemIsDark) => theme switch
    {
        WindowTheme.Light => false,
        WindowTheme.Dark => true,
        _ => systemIsDark(),
    };
}
