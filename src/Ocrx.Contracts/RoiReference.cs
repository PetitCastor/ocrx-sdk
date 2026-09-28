namespace Ocrx.Contracts;

/// <summary>The resolution a plugin's ROI coordinates were calibrated in.</summary>
public readonly record struct RoiReference(int Width, int Height)
{
    /// <summary>The historical reference every pre-game-catalog plugin calibrated against.</summary>
    public static RoiReference Default => new(RoiScaler.ReferenceWidth, RoiScaler.ReferenceHeight);

    /// <summary>
    /// How ROIs follow a frame of another aspect ratio; <see cref="RoiScaleMode.Fit"/> unless set.
    /// An init property rather than a constructor parameter so code compiled against the two-argument
    /// constructor keeps binding to it.
    /// </summary>
    public RoiScaleMode ScaleMode { get; init; }

    public bool IsValid => Width > 0 && Height > 0;
}
