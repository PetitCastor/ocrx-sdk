namespace Ocrx.Contracts;

/// <summary>
/// Maps ROIs declared in reference-resolution coordinates (2560x1440 by default, the resolution
/// all pre-game-catalog regions were calibrated at) to actual frame pixels. The reference is
/// treated as a canvas fitted inside the frame — uniform scale (the smaller of the two axis
/// ratios), then centered — so a 16:9 frame lands on the same UI spots exactly as before, and any
/// other aspect ratio letterboxes or pillarboxes with symmetric bars instead of stretching one
/// axis independently of the other; see the banner in <see cref="DescribeFrame(int, int)"/>. A
/// game with its own calibration passes an explicit <see cref="RoiReference"/>; every overload
/// here falls back to <see cref="RoiReference.Default"/> when it isn't given one. Every member
/// shares one fit computation, <see cref="FitTransform"/>, so a full-rect mapping and a
/// single-axis one can never disagree.
/// </summary>
public static class RoiScaler
{
    public const int ReferenceWidth = 2560;
    public const int ReferenceHeight = 1440;

    /// <summary>Scales a reference-space ROI to frame space, clamped inside the frame.</summary>
    public static RoiRect ToFrame(RoiRect referenceRoi, int frameWidth, int frameHeight)
        => ToFrame(referenceRoi, frameWidth, frameHeight, RoiReference.Default);

    /// <summary>Scales a <paramref name="reference"/>-space ROI to frame space, clamped inside the frame.</summary>
    /// <remarks>
    /// There is deliberately no identity shortcut for the reference resolution: it would hand
    /// back a mis-configured out-of-bounds ROI unclamped, and the engine would take that
    /// straight to a bitmap crop. At the reference resolution the scale factors are exactly 1.0
    /// and every Math.Round below is exact, so the general path returns in-bounds ROIs unchanged
    /// anyway.
    /// </remarks>
    public static RoiRect ToFrame(RoiRect referenceRoi, int frameWidth, int frameHeight, RoiReference reference)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frameWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frameHeight);
        if (!reference.IsValid)
            throw new ArgumentOutOfRangeException(nameof(reference), "Reference size must be positive.");

        // Uniform scale (the smaller of the two axis ratios) plus centering: the reference is a
        // canvas fitted inside the frame, with leftover space split into symmetric bars, rather
        // than each axis stretching independently to fill the frame.
        var fit = FitTransform.For(frameWidth, frameHeight, reference);

        // Scale edges rather than width/height so adjacent ROIs stay adjacent after rounding.
        var x = (uint)Math.Clamp(Math.Round(fit.MapX(referenceRoi.X)), 0, Math.Max(0, frameWidth - 1));
        var y = (uint)Math.Clamp(Math.Round(fit.MapY(referenceRoi.Y)), 0, Math.Max(0, frameHeight - 1));
        var right = (uint)Math.Clamp(Math.Round(fit.MapX(referenceRoi.X + referenceRoi.Width)), x + 1, frameWidth);
        var bottom = (uint)Math.Clamp(Math.Round(fit.MapY(referenceRoi.Y + referenceRoi.Height)), y + 1, frameHeight);

        return new RoiRect(x, y, right - x, bottom - y);
    }

    /// <summary>Scales a reference-space X coordinate (e.g. a pixel sample column) to frame space.</summary>
    /// <remarks>
    /// Under fit, X depends on frame height too — the scale is the smaller of the two axis ratios
    /// and X carries a centering offset whenever the frame is width-bound. This overload cannot see
    /// <paramref name="frameWidth"/>'s companion height, so it assumes a reference-aspect frame
    /// (implying the height from <paramref name="frameWidth"/>), which is exactly correct for 16:9
    /// callers and silently wrong for any other aspect.
    /// </remarks>
    [Obsolete("X depends on frame height too under fit. Use ToFrameX(int, int, int), which takes the " +
        "full frame size. This overload assumes a reference-aspect frame.")]
    public static int ToFrameX(int referenceX, int frameWidth)
        => ToFrameX(referenceX, frameWidth, RoiReference.Default);

    /// <summary>Scales a <paramref name="reference"/>-space X coordinate to frame space, assuming a reference-aspect frame.</summary>
    /// <remarks>See the remarks on <see cref="ToFrameX(int, int)"/>.</remarks>
    [Obsolete("X depends on frame height too under fit. Use ToFrameX(int, int, int, RoiReference), " +
        "which takes the full frame size. This overload assumes a reference-aspect frame.")]
    public static int ToFrameX(int referenceX, int frameWidth, RoiReference reference)
    {
        if (!reference.IsValid)
            throw new ArgumentOutOfRangeException(nameof(reference), "Reference size must be positive.");

        return ToFrameX(referenceX, frameWidth, ImpliedFrameHeight(frameWidth, reference), reference);
    }

    /// <summary>Scales a reference-space X coordinate to frame space.</summary>
    public static int ToFrameX(int referenceX, int frameWidth, int frameHeight)
        => ToFrameX(referenceX, frameWidth, frameHeight, RoiReference.Default);

    /// <summary>Scales a <paramref name="reference"/>-space X coordinate to frame space.</summary>
    public static int ToFrameX(int referenceX, int frameWidth, int frameHeight, RoiReference reference)
    {
        if (!reference.IsValid)
            throw new ArgumentOutOfRangeException(nameof(reference), "Reference size must be positive.");

        var fit = FitTransform.For(frameWidth, frameHeight, reference);
        return (int)Math.Round(fit.MapX(referenceX));
    }

    /// <summary>Scales a reference-space Y coordinate to frame space.</summary>
    /// <remarks>See the remarks on <see cref="ToFrameX(int, int)"/>; the same reasoning applies to Y
    /// and frame width.</remarks>
    [Obsolete("Y depends on frame width too under fit. Use ToFrameY(int, int, int), which takes the " +
        "full frame size. This overload assumes a reference-aspect frame.")]
    public static int ToFrameY(int referenceY, int frameHeight)
        => ToFrameY(referenceY, frameHeight, RoiReference.Default);

    /// <summary>Scales a <paramref name="reference"/>-space Y coordinate to frame space, assuming a reference-aspect frame.</summary>
    /// <remarks>See the remarks on <see cref="ToFrameY(int, int)"/>.</remarks>
    [Obsolete("Y depends on frame width too under fit. Use ToFrameY(int, int, int, RoiReference), " +
        "which takes the full frame size. This overload assumes a reference-aspect frame.")]
    public static int ToFrameY(int referenceY, int frameHeight, RoiReference reference)
    {
        if (!reference.IsValid)
            throw new ArgumentOutOfRangeException(nameof(reference), "Reference size must be positive.");

        return ToFrameY(referenceY, ImpliedFrameWidth(frameHeight, reference), frameHeight, reference);
    }

    /// <summary>Scales a reference-space Y coordinate to frame space.</summary>
    public static int ToFrameY(int referenceY, int frameWidth, int frameHeight)
        => ToFrameY(referenceY, frameWidth, frameHeight, RoiReference.Default);

    /// <summary>Scales a <paramref name="reference"/>-space Y coordinate to frame space.</summary>
    public static int ToFrameY(int referenceY, int frameWidth, int frameHeight, RoiReference reference)
    {
        if (!reference.IsValid)
            throw new ArgumentOutOfRangeException(nameof(reference), "Reference size must be positive.");

        var fit = FitTransform.For(frameWidth, frameHeight, reference);
        return (int)Math.Round(fit.MapY(referenceY));
    }

    /// <summary>The frame height implied by a reference-aspect frame of the given width.</summary>
    private static int ImpliedFrameHeight(int frameWidth, RoiReference reference)
        => (int)Math.Round(frameWidth * (double)reference.Height / reference.Width);

    /// <summary>The frame width implied by a reference-aspect frame of the given height.</summary>
    private static int ImpliedFrameWidth(int frameHeight, RoiReference reference)
        => (int)Math.Round(frameHeight * (double)reference.Width / reference.Height);

    /// <summary>One-line description of the capture size and how ROIs will be mapped to it.</summary>
    public static string DescribeFrame(int frameWidth, int frameHeight)
        => DescribeFrame(frameWidth, frameHeight, RoiReference.Default);

    /// <summary>One-line description of the capture size and how ROIs will be mapped to it against <paramref name="reference"/>.</summary>
    public static string DescribeFrame(int frameWidth, int frameHeight, RoiReference reference)
    {
        if (!reference.IsValid)
            throw new ArgumentOutOfRangeException(nameof(reference), "Reference size must be positive.");

        if (frameWidth == reference.Width && frameHeight == reference.Height)
            return $"capture {frameWidth}x{frameHeight} (reference resolution, ROIs used 1:1)";

        // One uniform factor, not two: fit scales both axes by the smaller ratio and absorbs the
        // difference into the centering offset. Reporting the axis ratios separately would describe
        // a per-axis distortion that no longer happens. Same FitTransform as ToFrame and the axis
        // helpers, so this can't drift back to reporting two ratios the way SDK-02's review caught.
        var fit = FitTransform.For(frameWidth, frameHeight, reference);
        var text = $"capture {frameWidth}x{frameHeight}, ROIs scaled x{fit.Scale:0.###}";

        if (frameWidth * (long)reference.Height != frameHeight * (long)reference.Width)
            text += $" — off-aspect: reference fitted and centered ({fit.OffsetX:0.#}px pillarbox, {fit.OffsetY:0.#}px letterbox)";
        return text;
    }
}
