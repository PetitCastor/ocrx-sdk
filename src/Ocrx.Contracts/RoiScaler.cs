namespace Ocrx.Contracts;

/// <summary>
/// Maps ROIs declared in reference-resolution coordinates (2560x1440 by default, the resolution
/// all pre-game-catalog regions were calibrated at) to actual frame pixels. The reference is
/// treated as a canvas fitted inside the frame — uniform scale (the smaller of the two axis
/// ratios), then centered — so a 16:9 frame lands on the same UI spots exactly as before, and any
/// other aspect ratio letterboxes or pillarboxes with symmetric bars instead of stretching one
/// axis independently of the other; see the banner in <see cref="DescribeFrame(int, int)"/>. A
/// game with its own calibration passes an explicit <see cref="RoiReference"/>; every overload
/// here falls back to <see cref="RoiReference.Default"/> when it isn't given one.
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
        var scale = Math.Min((double)frameWidth / reference.Width, (double)frameHeight / reference.Height);
        var contentWidth = reference.Width * scale;
        var contentHeight = reference.Height * scale;
        var offsetX = (frameWidth - contentWidth) / 2.0;
        var offsetY = (frameHeight - contentHeight) / 2.0;

        // Scale edges rather than width/height so adjacent ROIs stay adjacent after rounding.
        var x = (uint)Math.Clamp(Math.Round(offsetX + referenceRoi.X * scale), 0, Math.Max(0, frameWidth - 1));
        var y = (uint)Math.Clamp(Math.Round(offsetY + referenceRoi.Y * scale), 0, Math.Max(0, frameHeight - 1));
        var right = (uint)Math.Clamp(Math.Round(offsetX + (referenceRoi.X + referenceRoi.Width) * scale), x + 1, frameWidth);
        var bottom = (uint)Math.Clamp(Math.Round(offsetY + (referenceRoi.Y + referenceRoi.Height) * scale), y + 1, frameHeight);

        return new RoiRect(x, y, right - x, bottom - y);
    }

    /// <summary>Scales a reference-space X coordinate (e.g. a pixel sample column) to frame space.</summary>
    public static int ToFrameX(int referenceX, int frameWidth)
        => ToFrameX(referenceX, frameWidth, RoiReference.Default);

    /// <summary>Scales a <paramref name="reference"/>-space X coordinate to frame space.</summary>
    public static int ToFrameX(int referenceX, int frameWidth, RoiReference reference)
    {
        if (!reference.IsValid)
            throw new ArgumentOutOfRangeException(nameof(reference), "Reference size must be positive.");

        return (int)Math.Round((double)referenceX * frameWidth / reference.Width);
    }

    /// <summary>Scales a reference-space Y coordinate to frame space.</summary>
    public static int ToFrameY(int referenceY, int frameHeight)
        => ToFrameY(referenceY, frameHeight, RoiReference.Default);

    /// <summary>Scales a <paramref name="reference"/>-space Y coordinate to frame space.</summary>
    public static int ToFrameY(int referenceY, int frameHeight, RoiReference reference)
    {
        if (!reference.IsValid)
            throw new ArgumentOutOfRangeException(nameof(reference), "Reference size must be positive.");

        return (int)Math.Round((double)referenceY * frameHeight / reference.Height);
    }

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
        // a per-axis distortion that no longer happens.
        var scale = Math.Min((double)frameWidth / reference.Width, (double)frameHeight / reference.Height);
        var text = $"capture {frameWidth}x{frameHeight}, ROIs scaled x{scale:0.###}";

        if (frameWidth * (long)reference.Height != frameHeight * (long)reference.Width)
        {
            var barX = (frameWidth - reference.Width * scale) / 2.0;
            var barY = (frameHeight - reference.Height * scale) / 2.0;
            text += $" — off-aspect: reference fitted and centered ({barX:0.#}px pillarbox, {barY:0.#}px letterbox)";
        }
        return text;
    }
}
