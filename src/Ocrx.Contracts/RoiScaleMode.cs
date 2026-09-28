namespace Ocrx.Contracts;

/// <summary>
/// How a game's reference-space ROIs follow a frame whose aspect ratio differs from the
/// reference's. It describes the game's HUD layout, so it belongs to the game (the games catalog's
/// <c>reference</c> entry), not to any one plugin.
/// </summary>
/// <remarks>
/// The two modes agree on every frame at least as wide as the reference aspect: both scale by the
/// height ratio and center horizontally. They differ only on a frame taller than the reference
/// (16:10 or 4:3 against a 16:9 reference), which is where a game's own layout rule shows.
/// </remarks>
public enum RoiScaleMode
{
    /// <summary>
    /// The reference canvas is fitted inside the frame: uniform scale by the smaller axis ratio, then
    /// centered, letterboxing a taller frame. The default, and the behaviour before this mode existed.
    /// </summary>
    Fit = 0,

    /// <summary>
    /// Scale by the height ratio alone and center horizontally, whatever the frame's width. A game
    /// that sizes its HUD to the screen height and keeps it centered — Star Citizen's scan badge,
    /// measured on 2026-09-28 at 1920x1200, 1920x1440 and 1600x1200 — lands here on a taller frame,
    /// where <see cref="Fit"/> would shrink the HUD and push it toward the vertical center.
    /// ROIs near the left or right edge can then fall outside a narrow frame; mapping clamps them.
    /// </summary>
    Height = 1,
}
