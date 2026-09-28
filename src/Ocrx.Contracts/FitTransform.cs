namespace Ocrx.Contracts;

/// <summary>
/// The single fit computation behind every <see cref="RoiScaler"/> member: a uniform scale plus the
/// offsets that place the reference canvas inside the frame, chosen by the reference's
/// <see cref="RoiReference.ScaleMode"/>. <see cref="RoiScaler.ToFrame(RoiRect, int, int, RoiReference)"/>
/// and the axis helpers all go through <see cref="For(int, int, RoiReference)"/>, so the scale and
/// offset math cannot drift between a full-rect mapping and a single-axis one.
/// </summary>
internal readonly record struct FitTransform(double Scale, double OffsetX, double OffsetY)
{
    /// <summary>Computes the fit of <paramref name="reference"/> inside a frame of the given size.</summary>
    public static FitTransform For(int frameWidth, int frameHeight, RoiReference reference)
    {
        var heightRatio = (double)frameHeight / reference.Height;
        var scale = reference.ScaleMode == RoiScaleMode.Height
            ? heightRatio
            : Math.Min((double)frameWidth / reference.Width, heightRatio);
        // Under Height on a frame narrower than the reference, OffsetX goes negative: the canvas
        // overhangs both sides equally and its center stays on the frame's center.
        var offsetX = (frameWidth - reference.Width * scale) / 2.0;
        var offsetY = (frameHeight - reference.Height * scale) / 2.0;
        return new FitTransform(scale, offsetX, offsetY);
    }

    /// <summary>Maps a reference-space X coordinate to frame space.</summary>
    public double MapX(double x) => OffsetX + x * Scale;

    /// <summary>Maps a reference-space Y coordinate to frame space.</summary>
    public double MapY(double y) => OffsetY + y * Scale;
}
