using Ocrx.Sdk;
using Ocrx.Sdk.Overlay;

namespace Ocrx.Sdk.Overlay.Tests;

public class OverlayPlacementTests
{
    private const int ScreenWidth = 2560;
    private const int ScreenHeight = 1440;
    private const int Width = 560;
    private const int Height = 88;
    private const int OffsetX = 10;
    private const int OffsetY = 36;

    [Fact]
    public void TopLeft_AnchorsAtTheOriginPlusOffset()
    {
        var (x, y) = Resolve(OverlayAnchor.TopLeft);

        Assert.Equal((10, 36), (x, y));
    }

    [Fact]
    public void TopCenter_AnchorsAtTheTopMidpointPlusOffset()
    {
        var (x, y) = Resolve(OverlayAnchor.TopCenter);

        Assert.Equal((1010, 36), (x, y));
    }

    [Fact]
    public void TopRight_AnchorsAtTheTopRightCornerPlusOffset()
    {
        var (x, y) = Resolve(OverlayAnchor.TopRight);

        Assert.Equal((2010, 36), (x, y));
    }

    [Fact]
    public void MiddleLeft_AnchorsAtTheLeftMidpointPlusOffset()
    {
        var (x, y) = Resolve(OverlayAnchor.MiddleLeft);

        Assert.Equal((10, 712), (x, y));
    }

    [Fact]
    public void Center_AnchorsAtTheScreenCenterPlusOffset()
    {
        var (x, y) = Resolve(OverlayAnchor.Center);

        Assert.Equal((1010, 712), (x, y));
    }

    [Fact]
    public void MiddleRight_AnchorsAtTheRightMidpointPlusOffset()
    {
        var (x, y) = Resolve(OverlayAnchor.MiddleRight);

        Assert.Equal((2010, 712), (x, y));
    }

    [Fact]
    public void BottomLeft_AnchorsAtTheBottomLeftCornerPlusOffset()
    {
        var (x, y) = Resolve(OverlayAnchor.BottomLeft);

        Assert.Equal((10, 1388), (x, y));
    }

    [Fact]
    public void BottomCenter_AnchorsAtTheBottomMidpointPlusOffset()
    {
        var (x, y) = Resolve(OverlayAnchor.BottomCenter);

        Assert.Equal((1010, 1388), (x, y));
    }

    [Fact]
    public void BottomRight_AnchorsAtTheBottomRightCornerPlusOffset()
    {
        var (x, y) = Resolve(OverlayAnchor.BottomRight);

        Assert.Equal((2010, 1388), (x, y));
    }

    [Fact]
    public void Custom_UsesTheCustomCoordinatesPlusOffset()
    {
        var (x, y) = OverlayPlacement.Resolve(
            OverlayAnchor.Custom, ScreenWidth, ScreenHeight, Width, Height,
            OffsetX, OffsetY, customX: 123, customY: 456);

        Assert.Equal((133, 492), (x, y));
    }

    [Fact]
    public void TopCenter_MatchesThePreExistingFormulaExactly()
    {
        var (x, y) = Resolve(OverlayAnchor.TopCenter);

        Assert.Equal(((ScreenWidth - Width) / 2 + OffsetX, 0 + OffsetY), (x, y));
    }

    private static (int X, int Y) Resolve(OverlayAnchor anchor)
        => OverlayPlacement.Resolve(
            anchor, ScreenWidth, ScreenHeight, Width, Height,
            OffsetX, OffsetY, customX: 0, customY: 0);
}
