using Ocrx.Sdk.Overlay;

namespace Ocrx.Sdk.Overlay.Tests;

public class OverlayVisibilityStateTests
{
    [Fact]
    public void NewState_IsHidden()
    {
        Assert.Equal(OverlayPhase.Hidden, new OverlayVisibilityState().Phase);
    }

    [Fact]
    public void MarkShown_MakesVisible()
    {
        var state = new OverlayVisibilityState();

        state.MarkShown();

        Assert.Equal(OverlayPhase.Visible, state.Phase);
    }

    [Fact]
    public void Clear_WhenVisible_BlanksAndDoesNotHide()
    {
        var state = new OverlayVisibilityState();
        state.MarkShown();

        Assert.Equal(OverlayAction.Blank, state.OnClear());
        Assert.Equal(OverlayPhase.Blank, state.Phase);
    }

    [Fact]
    public void Clear_WhenBlank_DoesNothingSoRepeatedClearsCannotRestartTheIdleCountdown()
    {
        var state = new OverlayVisibilityState();
        state.MarkShown();
        state.OnClear();

        Assert.Equal(OverlayAction.None, state.OnClear());
        Assert.Equal(OverlayAction.None, state.OnClear());
        Assert.Equal(OverlayPhase.Blank, state.Phase);
    }

    [Fact]
    public void Clear_WhenHidden_DoesNothing()
    {
        var state = new OverlayVisibilityState();

        Assert.Equal(OverlayAction.None, state.OnClear());
        Assert.Equal(OverlayPhase.Hidden, state.Phase);
    }

    [Fact]
    public void TimerExpiry_WhenVisible_Blanks()
    {
        var state = new OverlayVisibilityState();
        state.MarkShown();

        Assert.Equal(OverlayAction.Blank, state.OnTimerExpiry());
        Assert.Equal(OverlayPhase.Blank, state.Phase);
    }

    [Fact]
    public void TimerExpiry_WhenBlank_Hides()
    {
        var state = new OverlayVisibilityState();
        state.MarkShown();
        state.OnClear();

        Assert.Equal(OverlayAction.Hide, state.OnTimerExpiry());
        Assert.Equal(OverlayPhase.Hidden, state.Phase);
    }

    [Fact]
    public void TimerExpiry_WhenHidden_DoesNothing()
    {
        var state = new OverlayVisibilityState();

        Assert.Equal(OverlayAction.None, state.OnTimerExpiry());
        Assert.Equal(OverlayPhase.Hidden, state.Phase);
    }

    [Fact]
    public void ShowDuringBlank_ReturnsToVisibleAndNextClearBlanksAgain()
    {
        var state = new OverlayVisibilityState();
        state.MarkShown();
        state.OnClear();

        state.MarkShown();

        Assert.Equal(OverlayPhase.Visible, state.Phase);
        Assert.Equal(OverlayAction.Blank, state.OnClear());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MarkHidden_FromAnyShownPhase_SetsHidden(bool blankFirst)
    {
        var state = new OverlayVisibilityState();
        state.MarkShown();
        if (blankFirst)
            state.OnClear();

        state.MarkHidden();

        Assert.Equal(OverlayPhase.Hidden, state.Phase);
    }
}
