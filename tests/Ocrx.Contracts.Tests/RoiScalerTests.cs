using Ocrx.Contracts;
using Xunit;

namespace Ocrx.Contracts.Tests;

/// <summary>
/// The monolith's RoiScalerTests, now the only copy: same inputs, same expected outputs, on the
/// RoiRect port of the scaler. Every ROI in the system is placed by this arithmetic, so the cases
/// stay exactly as the monolith accumulated them rather than being re-derived from the code.
/// </summary>
public class RoiScalerTests
{
    private static readonly RoiRect SampleRoi = new(620, 640, 440, 340);

    [Fact]
    public void ToFrame_AtReferenceResolution_ReturnsRoiUnchanged()
    {
        var scaled = RoiScaler.ToFrame(SampleRoi, RoiScaler.ReferenceWidth, RoiScaler.ReferenceHeight);

        Assert.Equal(SampleRoi.X, scaled.X);
        Assert.Equal(SampleRoi.Y, scaled.Y);
        Assert.Equal(SampleRoi.Width, scaled.Width);
        Assert.Equal(SampleRoi.Height, scaled.Height);
    }

    [Fact]
    public void ToFrame_At1080p_ScalesByThreeQuarters()
    {
        var scaled = RoiScaler.ToFrame(SampleRoi, 1920, 1080);

        Assert.Equal(465u, scaled.X);      // 620 * 0.75
        Assert.Equal(480u, scaled.Y);      // 640 * 0.75
        Assert.Equal(330u, scaled.Width);  // right 1060*0.75=795, 795-465
        Assert.Equal(255u, scaled.Height); // bottom 980*0.75=735, 735-480
    }

    [Fact]
    public void ToFrame_At4K_ScalesUp()
    {
        var scaled = RoiScaler.ToFrame(SampleRoi, 3840, 2160);

        Assert.Equal(930u, scaled.X);
        Assert.Equal(960u, scaled.Y);
        Assert.Equal(660u, scaled.Width);
        Assert.Equal(510u, scaled.Height);
    }

    [Fact]
    public void ToFrame_AdjacentRois_StayAdjacentAfterScaling()
    {
        // Edge-based rounding: a ROI ending where another begins must not gap or overlap.
        var left = new RoiRect(100, 0, 233, 100);
        var right = new RoiRect(333, 0, 233, 100);

        var scaledLeft = RoiScaler.ToFrame(left, 1920, 1080);
        var scaledRight = RoiScaler.ToFrame(right, 1920, 1080);

        Assert.Equal(scaledLeft.X + scaledLeft.Width, scaledRight.X);
    }

    [Fact]
    public void ToFrame_RoiTouchingFrameEdge_StaysInsideFrame()
    {
        var edgeRoi = new RoiRect(2100, 1300, 460, 140);

        var scaled = RoiScaler.ToFrame(edgeRoi, 1920, 1080);

        Assert.True(scaled.X + scaled.Width <= 1920);
        Assert.True(scaled.Y + scaled.Height <= 1080);
        Assert.True(scaled.Width >= 1);
        Assert.True(scaled.Height >= 1);
    }

    [Fact]
    public void ToFrame_AtReferenceResolution_ClampsRoiThatOverflowsTheFrame()
    {
        // A mis-typed config value used to escape unclamped through the identity shortcut, and
        // the engine would hand it straight to a bitmap crop.
        var overflowing = new RoiRect(2500, 1400, 400, 200);

        var scaled = RoiScaler.ToFrame(overflowing, RoiScaler.ReferenceWidth, RoiScaler.ReferenceHeight);

        Assert.True(scaled.X + scaled.Width <= RoiScaler.ReferenceWidth);
        Assert.True(scaled.Y + scaled.Height <= RoiScaler.ReferenceHeight);
    }

    [Theory]
    [InlineData(0, 1440)]
    [InlineData(2560, 0)]
    [InlineData(-1920, 1080)]
    public void ToFrame_NonPositiveFrameSize_Throws(int frameWidth, int frameHeight)
    {
        // Clamping a rect into a zero-sized frame means Math.Clamp(v, 1, 0), which throws an
        // ArgumentException from deep inside the scaler; reject the frame size instead.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RoiScaler.ToFrame(SampleRoi, frameWidth, frameHeight));
    }

    [Fact]
    public void ToFrameX_ScalesReferenceColumn()
    {
        // The point-scaling overloads the pixel-strip ROIs use; the identity case has to survive
        // the same shortcut ToFrame takes.
        Assert.Equal(798, RoiScaler.ToFrameX(1064, 1920));
        Assert.Equal(1064, RoiScaler.ToFrameX(1064, RoiScaler.ReferenceWidth));
    }

    [Fact]
    public void ToFrameY_ScalesReferenceRow()
    {
        Assert.Equal(480, RoiScaler.ToFrameY(640, 1080));
    }

    [Fact]
    public void DescribeFrame_ReferenceResolution_SaysOneToOne()
    {
        Assert.Contains("1:1", RoiScaler.DescribeFrame(2560, 1440));
    }

    [Fact]
    public void DescribeFrame_Same16By9_NoAspectWarning()
    {
        // Scaling within 16:9 is the verified path, so the banner must not cry wolf about it.
        var text = RoiScaler.DescribeFrame(1920, 1080);

        Assert.Contains("scaled", text);
        Assert.DoesNotContain("WARNING", text);
    }

    [Fact]
    public void DescribeFrame_Ultrawide_WarnsAboutAspect()
    {
        // The non-16:9 case: per-axis scaling is unverified, so the banner must say so.
        Assert.Contains("WARNING", RoiScaler.DescribeFrame(3440, 1440));
    }

    // SignaturePlugin's counter ROI, the region this scaler exists to place correctly.
    private static readonly RoiRect CounterRoi = new(1264, 454, 70, 44);

    // Values are hardcoded rather than compared against the three-argument overload: that overload
    // forwards here with RoiReference.Default, so comparing the two would pass no matter what the
    // arithmetic did. These pin the arithmetic itself.
    //
    // These frames are all exactly 16:9, the reference aspect. Their expected values must survive
    // the stretch-to-fit change in SDK-02 untouched — fit and stretch agree exactly when the frame
    // matches the reference aspect, so any movement here is a regression, never an intended change.
    [Theory]
    [InlineData(1280, 720, 632u, 227u, 35u, 22u)]
    [InlineData(1600, 900, 790u, 284u, 44u, 27u)]
    [InlineData(1920, 1080, 948u, 340u, 52u, 34u)]
    [InlineData(2560, 1440, 1264u, 454u, 70u, 44u)]
    [InlineData(3840, 2160, 1896u, 681u, 105u, 66u)]
    [InlineData(7680, 4320, 3792u, 1362u, 210u, 132u)]
    public void ToFrame_AtSixteenByNine_PlacesTheCounterRoiIdentically(
        int frameWidth, int frameHeight, uint x, uint y, uint width, uint height)
    {
        var scaled = RoiScaler.ToFrame(CounterRoi, frameWidth, frameHeight, RoiReference.Default);

        Assert.Equal(new RoiRect(x, y, width, height), scaled);
        // Passing the default must remain indistinguishable from omitting it.
        Assert.Equal(RoiScaler.ToFrame(CounterRoi, frameWidth, frameHeight), scaled);
    }

    // Off-aspect frames. These carry today's per-axis (stretch) results, where X and Y scale by
    // different factors. SDK-02 replaces stretch with fit — uniform scale plus centering — and must
    // recompute every row in this theory. It is deliberately a separate method from the 16:9 one
    // above so that "did I change 16:9 behaviour?" is answered by which method you edited.
    //
    // 1366x768 earns its place: at 1.7786 it reads as 16:9 to a human and to any aspect check
    // written against a rounded ratio, but it is not (16:9 is 1.7778), so it must letterbox.
    [Theory]
    [InlineData(2560, 1080, 1264u, 340u, 70u, 34u)]   // 21:9
    [InlineData(3440, 1440, 1698u, 454u, 95u, 44u)]   // 21:9
    [InlineData(3840, 1080, 1896u, 340u, 105u, 34u)]  // 32:9
    [InlineData(5120, 1440, 2528u, 454u, 140u, 44u)]  // 32:9
    [InlineData(1920, 1200, 948u, 378u, 52u, 37u)]    // 16:10
    [InlineData(2560, 1600, 1264u, 504u, 70u, 49u)]   // 16:10
    [InlineData(1366, 768, 674u, 242u, 38u, 24u)]     // 1.7786, near-16:9 but not
    [InlineData(1920, 1440, 948u, 454u, 52u, 44u)]    // 4:3
    [InlineData(1024, 768, 506u, 242u, 28u, 24u)]     // 4:3
    public void ToFrame_OffAspect_ScalesEachAxisIndependently(
        int frameWidth, int frameHeight, uint x, uint y, uint width, uint height)
    {
        var scaled = RoiScaler.ToFrame(CounterRoi, frameWidth, frameHeight, RoiReference.Default);

        Assert.Equal(new RoiRect(x, y, width, height), scaled);
        Assert.Equal(RoiScaler.ToFrame(CounterRoi, frameWidth, frameHeight), scaled);
    }

    [Fact]
    public void ToFrame_NonDefaultReference_MapsProportionally()
    {
        var roi = new RoiRect(100, 100, 50, 50);

        var scaled = RoiScaler.ToFrame(roi, 2560, 1440, new RoiReference(1280, 720));

        Assert.Equal(200u, scaled.X);
        Assert.Equal(200u, scaled.Y);
        Assert.Equal(100u, scaled.Width);
        Assert.Equal(100u, scaled.Height);
    }

    [Theory]
    [InlineData(0, 1440)]
    [InlineData(2560, 0)]
    [InlineData(-2560, 1440)]
    [InlineData(2560, -1440)]
    public void ToFrame_InvalidReference_Throws(int referenceWidth, int referenceHeight)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RoiScaler.ToFrame(SampleRoi, 1920, 1080, new RoiReference(referenceWidth, referenceHeight)));
    }
}
