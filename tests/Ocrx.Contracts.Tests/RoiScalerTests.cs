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
        // the same shortcut ToFrame takes. Deliberately exercises the obsolete width-only overload
        // (SDK-03): it must keep returning these exact values, because it assumes a reference-aspect
        // frame and both frames here are 16:9.
#pragma warning disable CS0618 // obsolete-by-design: pinning the width-only overload's behaviour
        Assert.Equal(798, RoiScaler.ToFrameX(1064, 1920));
        Assert.Equal(1064, RoiScaler.ToFrameX(1064, RoiScaler.ReferenceWidth));
#pragma warning restore CS0618
    }

    [Fact]
    public void ToFrameY_ScalesReferenceRow()
    {
#pragma warning disable CS0618 // obsolete-by-design: pinning the width-only overload's behaviour
        Assert.Equal(480, RoiScaler.ToFrameY(640, 1080));
#pragma warning restore CS0618
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
        // The non-16:9 case is deliberately letterboxed now, not stretched per-axis, so the
        // banner reports the fit-and-center behaviour and the resulting bar size instead of
        // calling the positions unverified.
        var text = RoiScaler.DescribeFrame(3440, 1440);

        Assert.Contains("fitted and centered", text);
        // One applied factor, not two. At 3440x1440 fit is height-bound, so every ROI is scaled by
        // 1.0 — quoting the raw width ratio 1.344 here would describe a stretch that never happens
        // and contradict the "fitted and centered" half of the same sentence.
        Assert.Contains("scaled x1 ", text);
        Assert.DoesNotContain("1.344", text);
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

    // Off-aspect frames. SDK-02 replaces stretch with fit — uniform scale plus centering — so
    // every row here now carries the fit result rather than the old per-axis (stretch) one. It is
    // deliberately a separate method from the 16:9 one above so that "did I change 16:9
    // behaviour?" is answered by which method you edited.
    //
    // 1366x768 earns its place: at 1.7786 it reads as 16:9 to a human and to any aspect check
    // written against a rounded ratio, but it is not (16:9 is 1.7778) — yet its fit result is
    // pixel-identical to the old stretch result, because the two scales round to the same pixels
    // this close to the reference aspect. That is correct, not a missed update.
    //
    // Every ultrawide row keeps width 70 — the ROI's calibrated width — instead of growing to
    // 95/105/140 the way stretch used to smear it across a wider frame; fit only ever moves the
    // ROI, never resizes it.
    [Theory]
    [InlineData(2560, 1080, 1268u, 340u, 52u, 34u)]   // 21:9
    [InlineData(3440, 1440, 1704u, 454u, 70u, 44u)]   // 21:9
    [InlineData(3840, 1080, 1908u, 340u, 52u, 34u)]   // 32:9
    [InlineData(5120, 1440, 2544u, 454u, 70u, 44u)]   // 32:9
    [InlineData(1920, 1200, 948u, 400u, 52u, 34u)]    // 16:10
    [InlineData(2560, 1600, 1264u, 534u, 70u, 44u)]   // 16:10
    [InlineData(1366, 768, 674u, 242u, 38u, 24u)]     // 1.7786, near-16:9 but not — unchanged
    [InlineData(1920, 1440, 948u, 520u, 52u, 34u)]    // 4:3
    [InlineData(1024, 768, 506u, 278u, 28u, 17u)]     // 4:3
    public void ToFrame_OffAspect_FitsAndCentersTheCounterRoi(
        int frameWidth, int frameHeight, uint x, uint y, uint width, uint height)
    {
        var scaled = RoiScaler.ToFrame(CounterRoi, frameWidth, frameHeight, RoiReference.Default);

        Assert.Equal(new RoiRect(x, y, width, height), scaled);
        Assert.Equal(RoiScaler.ToFrame(CounterRoi, frameWidth, frameHeight), scaled);
    }

    [Fact]
    public void ToFrame_Pillarboxed_BarsAreSymmetric()
    {
        // 3440x1440 is wider than the 16:9 reference: scale is height-bound (1.0) and the leftover
        // width is split into equal bars on both sides. Mapping the whole reference canvas rather
        // than a corner pixel lets both bars be measured, so this pins the split itself — a one-sided
        // assertion would pass even if the far edge drifted.
        var canvas = RoiScaler.ToFrame(new RoiRect(0, 0, 2560, 1440), 3440, 1440);

        var leftBar = canvas.X;
        var rightBar = 3440u - (canvas.X + canvas.Width);

        Assert.Equal(440u, leftBar);
        Assert.Equal(leftBar, rightBar);
        Assert.Equal(0u, canvas.Y); // height-bound, so no letterbox on the other axis
    }

    [Fact]
    public void ToFrame_Letterboxed_BarsAreSymmetric()
    {
        // 1920x1200 is narrower (relatively taller) than the 16:9 reference: scale is width-bound
        // (0.75) and the leftover height is split into equal bars top and bottom. Same reasoning as
        // the pillarbox case — measure both bars, not just the near one.
        var canvas = RoiScaler.ToFrame(new RoiRect(0, 0, 2560, 1440), 1920, 1200);

        var topBar = canvas.Y;
        var bottomBar = 1200u - (canvas.Y + canvas.Height);

        Assert.Equal(60u, topBar);
        Assert.Equal(topBar, bottomBar);
        Assert.Equal(0u, canvas.X); // width-bound, so no pillarbox on the other axis
    }

    [Theory]
    [InlineData(2561, 1440)]   // 1px of leftover width, height-bound
    [InlineData(2563, 1440)]   // 3px
    [InlineData(2560, 1441)]   // 1px of leftover height, width-bound
    [InlineData(1921, 1080)]   // odd leftover at a real-world width
    public void ToFrame_OddLeftover_SplitsBarsWithinOnePixel(int frameWidth, int frameHeight)
    {
        // An odd number of leftover pixels has no symmetric split: one side must keep the extra
        // pixel. The two Symmetric tests above both land on even leftovers, so neither pins this.
        // What must hold is that the canvas keeps its full mapped size and the bars differ by at
        // most one — a drift in the centering offset shows up here as a larger gap.
        var canvas = RoiScaler.ToFrame(new RoiRect(0, 0, 2560, 1440), frameWidth, frameHeight);

        var leftBar = (int)canvas.X;
        var rightBar = frameWidth - (int)(canvas.X + canvas.Width);
        var topBar = (int)canvas.Y;
        var bottomBar = frameHeight - (int)(canvas.Y + canvas.Height);

        Assert.True(Math.Abs(leftBar - rightBar) <= 1, $"pillarbox bars {leftBar}/{rightBar}");
        Assert.True(Math.Abs(topBar - bottomBar) <= 1, $"letterbox bars {topBar}/{bottomBar}");
        Assert.True(leftBar >= 0 && rightBar >= 0 && topBar >= 0 && bottomBar >= 0);
    }

    [Fact]
    public void ToFrame_RoiAtReferenceFarEdge_StaysInsideTheCenteredCanvas()
    {
        // An ROI pinned to the reference's own far edge (X + Width == reference width) used to
        // stretch out toward the frame's actual edge. Under fit it must stay inside the centered
        // 2560-wide canvas, well short of the 3440-wide frame's edge.
        var edgeRoi = new RoiRect(2490, 454, 70, 44);

        var scaled = RoiScaler.ToFrame(edgeRoi, 3440, 1440);

        Assert.Equal(new RoiRect(2930, 454, 70, 44), scaled);
        Assert.True(scaled.X + scaled.Width < 3440);
    }

    [Fact]
    public void ToFrame_DegenerateOnePixelFrame_ReturnsAtLeastOneByOneWithoutThrowing()
    {
        var scaled = RoiScaler.ToFrame(CounterRoi, 1, 1);

        Assert.True(scaled.Width >= 1);
        Assert.True(scaled.Height >= 1);
        Assert.True(scaled.X + scaled.Width <= 1);
        Assert.True(scaled.Y + scaled.Height <= 1);
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

    // SDK-03: the fit-aware full-frame axis helpers.
    //
    // The agreement and round-trip theories below deliberately mix the six 16:9 frames with the
    // nine off-aspect ones from ToFrame_OffAspect_FitsAndCentersTheCounterRoi (21:9, 32:9, 16:10,
    // near-16:9, 4:3). At exactly 16:9 fit and the old per-axis stretch agree, so a 16:9-only theory
    // would pass against either implementation and prove nothing about the fit behaviour this task
    // adds. The off-aspect rows are the ones that fail under stretch: see the reasoning on each
    // theory below for the specific case that would have caught it.
    //
    // The obsolete-overload theory stays 16:9-only (below) — its documented contract *is* a
    // reference-aspect frame, so off-aspect frames are out of its contract, not a gap in its test.

    [Theory]
    [InlineData(1280, 720)]
    [InlineData(1600, 900)]
    [InlineData(1920, 1080)]
    [InlineData(2560, 1440)]
    [InlineData(3840, 2160)]
    [InlineData(7680, 4320)]
    [InlineData(2560, 1080)]   // 21:9
    [InlineData(3440, 1440)]   // 21:9
    [InlineData(3840, 1080)]   // 32:9
    [InlineData(5120, 1440)]   // 32:9
    [InlineData(1920, 1200)]   // 16:10
    [InlineData(2560, 1600)]   // 16:10
    [InlineData(1366, 768)]    // 1.7786, near-16:9 but not
    [InlineData(1920, 1440)]   // 4:3
    [InlineData(1024, 768)]    // 4:3
    public void ToFrameXY_FullFrame_AgreesWithToFrameOnSameRoi(int frameWidth, int frameHeight)
    {
        // ToFrameX/ToFrameY now go through the same FitTransform as ToFrame, so mapping a corner
        // via the axis helpers must land on exactly the pixel ToFrame places that corner at.
        //
        // This is the theory that catches a regression back to per-axis stretch: at 3440x1440,
        // ToFrame (fit) places CounterRoi.X at 1704, but the old stretch formula
        // (referenceX * frameWidth / reference.Width, ignoring height entirely) gives
        // round(1264 * 3440 / 2560) = round(1698.5) = 1698 — Math.Round defaults to
        // MidpointRounding.ToEven, so the exact .5 rounds down to the even 1698, not up — a 6px
        // miss that fails the assertion below. Every 16:9 row in this theory would pass unchanged
        // under either implementation.
        var scaledRoi = RoiScaler.ToFrame(CounterRoi, frameWidth, frameHeight);

        var x = RoiScaler.ToFrameX((int)CounterRoi.X, frameWidth, frameHeight);
        var y = RoiScaler.ToFrameY((int)CounterRoi.Y, frameWidth, frameHeight);

        Assert.Equal((int)scaledRoi.X, x);
        Assert.Equal((int)scaledRoi.Y, y);
    }

    [Theory]
    [InlineData(1280, 720, 632, 227)]
    [InlineData(1600, 900, 790, 284)]
    [InlineData(1920, 1080, 948, 340)]
    [InlineData(2560, 1440, 1264, 454)]
    [InlineData(3840, 2160, 1896, 681)]
    [InlineData(7680, 4320, 3792, 1362)]
    public void ObsoleteToFrameXY_KeepsSdk02SixteenByNineValues(
        int frameWidth, int frameHeight, int expectedX, int expectedY)
    {
        // The width-only overloads are obsolete, not gone: a caller who never updates keeps getting
        // the pre-SDK-03 answer for every 16:9 frame, because the implied-height delegation is exact
        // at the reference aspect.
#pragma warning disable CS0618 // deliberately exercising the obsolete overload
        var x = RoiScaler.ToFrameX((int)CounterRoi.X, frameWidth);
        var y = RoiScaler.ToFrameY((int)CounterRoi.Y, frameHeight);
#pragma warning restore CS0618

        Assert.Equal(expectedX, x);
        Assert.Equal(expectedY, y);
    }

    [Theory]
    [InlineData(1280, 720)]
    [InlineData(1600, 900)]
    [InlineData(1920, 1080)]
    [InlineData(2560, 1440)]
    [InlineData(3840, 2160)]
    [InlineData(7680, 4320)]
    [InlineData(2560, 1080)]   // 21:9
    [InlineData(3440, 1440)]   // 21:9
    [InlineData(3840, 1080)]   // 32:9
    [InlineData(5120, 1440)]   // 32:9
    [InlineData(1920, 1200)]   // 16:10
    [InlineData(2560, 1600)]   // 16:10
    [InlineData(1366, 768)]    // 1.7786, near-16:9 but not
    [InlineData(1920, 1440)]   // 4:3
    [InlineData(1024, 768)]    // 4:3
    public void ToFrameXY_FullFrame_RoundTripsWithinOnePixel(int frameWidth, int frameHeight)
    {
        // General fit inversion — the uniform scale is the smaller of the two axis ratios, with a
        // centering offset whenever the frame isn't the reference aspect (both zero at 16:9, where
        // this collapses to the simple scale-only inversion). Computed independently of RoiScaler's
        // own arithmetic, using only the reference constants and frame size, so this also catches a
        // regression in the axis helpers' *offset* handling, not only their scale.
        //
        // At 3440x1440 this is the theory that catches a regression back to per-axis stretch: the
        // old ToFrameX gives 1698 (see the reasoning on ToFrameXY_FullFrame_AgreesWithToFrameOnSameRoi),
        // which inverts back to (1698 - 440) / 1 = 1258 — 6px off from CounterRoi.X (1264), failing
        // the <= 1 assertion below. Every 16:9 row would round-trip cleanly under either
        // implementation.
        var scale = Math.Min(
            (double)frameWidth / RoiScaler.ReferenceWidth, (double)frameHeight / RoiScaler.ReferenceHeight);
        var offsetX = (frameWidth - RoiScaler.ReferenceWidth * scale) / 2.0;
        var offsetY = (frameHeight - RoiScaler.ReferenceHeight * scale) / 2.0;

        var frameX = RoiScaler.ToFrameX((int)CounterRoi.X, frameWidth, frameHeight);
        var frameY = RoiScaler.ToFrameY((int)CounterRoi.Y, frameWidth, frameHeight);

        var roundTrippedX = (frameX - offsetX) / scale;
        var roundTrippedY = (frameY - offsetY) / scale;

        Assert.True(Math.Abs(roundTrippedX - CounterRoi.X) <= 1);
        Assert.True(Math.Abs(roundTrippedY - CounterRoi.Y) <= 1);
    }

    // SDK-01's injected reference, on the members other than ToFrame.
    //
    // Every case below picks a 1280x720 reference against a 2560x1440 frame, where the injected
    // reference gives exactly double the default's answer. That is the point: an overload that
    // quietly ignored its RoiReference and fell back to RoiReference.Default would return the
    // reference-space value unchanged and fail, so these pin the plumbing itself rather than
    // re-testing the fit arithmetic ToFrame's own tests already cover.

    [Fact]
    public void ToFrameXY_NonDefaultReference_ScalesAgainstThatReference()
    {
        var x = RoiScaler.ToFrameX(100, 2560, 1440, new RoiReference(1280, 720));
        var y = RoiScaler.ToFrameY(100, 2560, 1440, new RoiReference(1280, 720));

        Assert.Equal(200, x);
        Assert.Equal(200, y);
        Assert.NotEqual(RoiScaler.ToFrameX(100, 2560, 1440), x);
    }

    [Fact]
    public void ObsoleteToFrameXY_NonDefaultReference_ImpliesTheOtherDimensionFromIt()
    {
        // The implied-dimension delegation must use the injected reference's aspect, not 16:9's.
        // Here they coincide numerically, but the scale does not: a fallback to RoiReference.Default
        // would give 100, not 200.
#pragma warning disable CS0618 // deliberately exercising the obsolete overload
        var x = RoiScaler.ToFrameX(100, 2560, new RoiReference(1280, 720));
        var y = RoiScaler.ToFrameY(100, 1440, new RoiReference(1280, 720));
#pragma warning restore CS0618

        Assert.Equal(200, x);
        Assert.Equal(200, y);
    }

    [Fact]
    public void DescribeFrame_NonDefaultReference_DescribesThatReference()
    {
        var atReference = RoiScaler.DescribeFrame(1280, 720, new RoiReference(1280, 720));
        var scaled = RoiScaler.DescribeFrame(2560, 1440, new RoiReference(1280, 720));

        // 1280x720 is the injected reference, so it is the 1:1 case — against the default it would
        // report a x0.5 downscale instead.
        Assert.Contains("1:1", atReference);
        Assert.Contains("x2", scaled);
        Assert.DoesNotContain("off-aspect", scaled); // same aspect as the injected reference
    }

    [Theory]
    [InlineData(0, 720)]
    [InlineData(1280, 0)]
    [InlineData(-1280, 720)]
    [InlineData(1280, -720)]
    public void AxisHelpersAndDescribeFrame_InvalidReference_Throw(int referenceWidth, int referenceHeight)
    {
        var reference = new RoiReference(referenceWidth, referenceHeight);

        Assert.Throws<ArgumentOutOfRangeException>(() => RoiScaler.ToFrameX(100, 1920, 1080, reference));
        Assert.Throws<ArgumentOutOfRangeException>(() => RoiScaler.ToFrameY(100, 1920, 1080, reference));
        Assert.Throws<ArgumentOutOfRangeException>(() => RoiScaler.DescribeFrame(1920, 1080, reference));
#pragma warning disable CS0618 // the obsolete overloads validate the reference before delegating
        Assert.Throws<ArgumentOutOfRangeException>(() => RoiScaler.ToFrameX(100, 1920, reference));
        Assert.Throws<ArgumentOutOfRangeException>(() => RoiScaler.ToFrameY(100, 1080, reference));
#pragma warning restore CS0618
    }
}
