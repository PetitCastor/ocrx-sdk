namespace Ocrx.Sdk.Tests;

/// <summary>Completes a signal on the first line written, so a test awaits the log
/// deterministically instead of polling.</summary>
internal sealed class SignalOutput(TaskCompletionSource signal) : IPluginOutput
{
    public string Last { get; private set; } = "";

    public void WriteLine(string message = "")
    {
        Last = message;
        signal.TrySetResult();
    }

    public void UpdateStatus(string statusText) { }
}
