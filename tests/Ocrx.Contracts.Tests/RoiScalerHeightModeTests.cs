using Ocrx.Contracts;
using Xunit;

namespace Ocrx.Contracts.Tests;

/// <summary>
/// <see cref="RoiScaleMode.Height"/> against the Star Citizen captures of 2026-09-28. The ROI is
/// SignaturePlugin's RS counter, <c>(1264, 454, 70, 44)</c> in 2560x1440 reference space, and the
/// digit boxes are what the engine actually captured, converted to game (client) pixels.
/// </summary>
public class RoiScalerHeightModeTests
{
    private static readonly RoiRect Counter = new(1264, 454, 70, 44);
    private static readonly RoiReference Height = RoiReference.Default with { ScaleMode = RoiScaleMode.Height };

    private static bool Contains(RoiRect roi, int left, int top, int right, int bottom)
        => roi.X <= left && roi.Y <= top && roi.X + roi.Width > right && roi.Y + roi.Height > bottom;

    [Fact]
    public void ScaleMode_DefaultsToFit()
    {
        Assert.Equal(RoiScaleMode.Fit, RoiReference.Default.ScaleMode);
        Assert.Equal(RoiScaleMode.Fit, new RoiReference(2560, 1440).ScaleMode);
    }

    [Fact]
    public void ScaleMode_IsPartOfEquality()
    {
        // The engine caches a client's reference and compares it; a mode change must not look equal.
        Assert.NotEqual(RoiReference.Default, Height);
    }

    [Fact]
    public void Height_At1600x1200_CoversTheDigitsStarCitizenDrew()
    {
        var mapped = RoiScaler.ToFrame(Counter, 1600, 1200, Height);

        Assert.Equal(new RoiRect(787, 378, 58, 37), mapped);
        // Windowed 1600x1200: digits at x 803..824, y 389..396 inside the client.
        Assert.True(Contains(mapped, 803, 389, 824, 396), $"{mapped} misses the badge digits");
    }

    [Fact]
    public void Fit_At1600x1200_MissesTheDigits()
    {
        // The reason Height exists: fit letterboxes a 4:3 frame and shrinks the canvas, pushing the
        // ROI about 45 px below where the game drew the badge.
        var mapped = RoiScaler.ToFrame(Counter, 1600, 1200);

        Assert.Equal(new RoiRect(790, 434, 44, 27), mapped);
        Assert.False(Contains(mapped, 803, 389, 824, 396));
    }

    [Fact]
    public void Height_At1920x1440_KeepsTheReferenceSizeAndCentersIt()
    {
        var mapped = RoiScaler.ToFrame(Counter, 1920, 1440, Height);

        // Scale 1.0, canvas overhanging 320 px on each side.
        Assert.Equal(new RoiRect(944, 454, 70, 44), mapped);
        // 1920x1440: digits at x 964..992, y 470..479.
        Assert.True(Contains(mapped, 964, 470, 992, 479), $"{mapped} misses the badge digits");
    }

    [Theory]
    [InlineData(2560, 1440)] // the reference itself
    [InlineData(1920, 1080)] // 16:9
    [InlineData(3840, 2160)] // 16:9
    [InlineData(2560, 1080)] // 21:9
    [InlineData(3440, 1440)] // 21:9
    [InlineData(5120, 1440)] // 32:9
    public void Height_OnAFrameAtLeastAsWideAsTheReference_MatchesFit(int width, int height)
    {
        Assert.Equal(RoiScaler.ToFrame(Counter, width, height), RoiScaler.ToFrame(Counter, width, height, Height));
    }

    [Fact]
    public void Height_AxisHelpers_AgreeWithToFrame()
    {
        var mapped = RoiScaler.ToFrame(Counter, 1600, 1200, Height);

        Assert.Equal((int)mapped.X, RoiScaler.ToFrameX((int)Counter.X, 1600, 1200, Height));
        Assert.Equal((int)mapped.Y, RoiScaler.ToFrameY((int)Counter.Y, 1600, 1200, Height));
    }

    [Fact]
    public void Height_ARoiOffTheSideOfANarrowFrame_IsClampedInsideIt()
    {
        // The 4:3 frame shows only the reference's middle 1920 columns; a ROI at the reference's left
        // edge maps off-frame and must still come back as a valid in-frame rect, not throw.
        var mapped = RoiScaler.ToFrame(new RoiRect(0, 0, 100, 100), 1600, 1200, Height);

        Assert.Equal(0u, mapped.X);
        Assert.True(mapped.Width >= 1);
        Assert.True(mapped.X + mapped.Width <= 1600);
    }

    [Fact]
    public void DescribeFrame_UnderHeight_NamesTheMode()
    {
        var text = RoiScaler.DescribeFrame(1600, 1200, Height);

        Assert.Contains("scaled to height", text);
        Assert.DoesNotContain("letterbox", text);
    }
}
