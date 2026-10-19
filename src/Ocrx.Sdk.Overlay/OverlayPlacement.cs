namespace Ocrx.Sdk.Overlay;

/// <summary>Pure pixel math for resolving an <see cref="OverlayAnchor"/> to a window position.</summary>
internal static class OverlayPlacement
{
    /// <summary>
    /// Resolve the top-left corner (in physical pixels) of a <paramref name="width"/> x
    /// <paramref name="height"/> box anchored on a <paramref name="screenWidth"/> x
    /// <paramref name="screenHeight"/> screen, then apply the offset.
    /// </summary>
    /// <remarks>
    /// On an axis pinned to a screen edge the offset is a margin from that edge: a positive value
    /// always moves the box inward, so one <c>offsetY</c> keeps a bottom-anchored box on screen as
    /// well as a top-anchored one. On a centred axis, and for <see cref="OverlayAnchor.Custom"/>,
    /// the offset is additive.
    /// </remarks>
    public static (int X, int Y) Resolve(
        OverlayAnchor anchor, int screenWidth, int screenHeight,
        int width, int height, int offsetX, int offsetY, int customX, int customY)
    {
        var left = offsetX;
        var center = ((screenWidth - width) / 2) + offsetX;
        var right = screenWidth - width - offsetX;
        var top = offsetY;
        var middle = ((screenHeight - height) / 2) + offsetY;
        var bottom = screenHeight - height - offsetY;

        return anchor switch
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
            OverlayAnchor.Custom => (customX + offsetX, customY + offsetY),
            _ => (customX + offsetX, customY + offsetY),
        };
    }
}
