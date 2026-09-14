namespace Ocrx.Sdk.Overlay;

/// <summary>Pure pixel math for resolving an <see cref="OverlayAnchor"/> to a window position.</summary>
internal static class OverlayPlacement
{
    /// <summary>
    /// Resolve the top-left corner (in physical pixels) of a <paramref name="width"/> x
    /// <paramref name="height"/> box anchored on a <paramref name="screenWidth"/> x
    /// <paramref name="screenHeight"/> screen, then apply the additive offset.
    /// </summary>
    public static (int X, int Y) Resolve(
        OverlayAnchor anchor, int screenWidth, int screenHeight,
        int width, int height, int offsetX, int offsetY, int customX, int customY)
    {
        var left = 0;
        var center = (screenWidth - width) / 2;
        var right = screenWidth - width;
        var top = 0;
        var middle = (screenHeight - height) / 2;
        var bottom = screenHeight - height;

        var (x, y) = anchor switch
        {
            OverlayAnchor.TopLeft => (left, top),
            OverlayAnchor.TopCenter => (center, top),
            OverlayAnchor.TopRight => (right, top),
            OverlayAnchor.MiddleLeft => (left, middle),
            OverlayAnchor.Center => (center, middle),
            OverlayAnchor.MiddleRight => (right, middle),
            OverlayAnchor.BottomLeft => (left, bottom),
            OverlayAnchor.BottomCenter => (center, bottom),
            OverlayAnchor.BottomRight => (right, bottom),
            OverlayAnchor.Custom => (customX, customY),
            _ => (customX, customY),
        };

        return (x + offsetX, y + offsetY);
    }
}
