namespace Ocrx.Sdk;

/// <summary>Supported overlay positioning modes.</summary>
public enum OverlayAnchor
{
    /// <summary>Centre horizontally at the top of the primary screen.</summary>
    TopCenter = 0,

    /// <summary>Use <see cref="OverlaySpec.X"/> and <see cref="OverlaySpec.Y"/>.</summary>
    Custom = 1,

    /// <summary>Align to the top-left corner of the primary screen.</summary>
    TopLeft = 2,

    /// <summary>Align to the top-right corner of the primary screen.</summary>
    TopRight = 3,

    /// <summary>Centre vertically at the left edge of the primary screen.</summary>
    MiddleLeft = 4,

    /// <summary>Centre on both axes of the primary screen.</summary>
    Center = 5,

    /// <summary>Centre vertically at the right edge of the primary screen.</summary>
    MiddleRight = 6,

    /// <summary>Align to the bottom-left corner of the primary screen.</summary>
    BottomLeft = 7,

    /// <summary>Centre horizontally at the bottom of the primary screen.</summary>
    BottomCenter = 8,

    /// <summary>Align to the bottom-right corner of the primary screen.</summary>
    BottomRight = 9,
}
