namespace Ocrx.Contracts;

/// <summary>The resolution a plugin's ROI coordinates were calibrated in.</summary>
public readonly record struct RoiReference(int Width, int Height)
{
    /// <summary>The historical reference every pre-game-catalog plugin calibrated against.</summary>
    public static RoiReference Default => new(RoiScaler.ReferenceWidth, RoiScaler.ReferenceHeight);

    public bool IsValid => Width > 0 && Height > 0;
}
