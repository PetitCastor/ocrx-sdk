namespace Ocrx.Sdk.Overlay;

internal sealed class OverlayVisibilityState
{
    public OverlayPhase Phase { get; private set; }

    public void MarkShown() => Phase = OverlayPhase.Visible;

    public void MarkHidden() => Phase = OverlayPhase.Hidden;

    // A clear while already blank does nothing, so repeated clears cannot restart the idle countdown.
    public OverlayAction OnClear()
    {
        if (Phase != OverlayPhase.Visible)
            return OverlayAction.None;

        Phase = OverlayPhase.Blank;
        return OverlayAction.Blank;
    }

    // The one live timer means linger expiry while visible and idle expiry while blank.
    public OverlayAction OnTimerExpiry()
    {
        switch (Phase)
        {
            case OverlayPhase.Visible:
                Phase = OverlayPhase.Blank;
                return OverlayAction.Blank;
            case OverlayPhase.Blank:
                Phase = OverlayPhase.Hidden;
                return OverlayAction.Hide;
            default:
                return OverlayAction.None;
        }
    }
}
