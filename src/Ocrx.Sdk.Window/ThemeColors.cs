using System.Drawing;

namespace Ocrx.Sdk.Window;

internal static class ThemeColors
{
    internal static Color Back(bool dark) => dark ? Color.FromArgb(0x0f, 0x17, 0x2a) : Color.FromArgb(0xf8, 0xfa, 0xfc);

    internal static Color Fore(bool dark) => dark ? Color.FromArgb(0xe2, 0xe8, 0xf0) : Color.FromArgb(0x0f, 0x17, 0x2a);
}
