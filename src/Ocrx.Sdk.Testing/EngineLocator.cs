namespace Ocrx.Sdk.Testing;

/// <summary>Finds a built <c>Ocrx.Engine.exe</c> for <see cref="ReplayHarness"/> to spawn.</summary>
public static class EngineLocator
{
    private const string EnvVar = "OCRX_ENGINE_PATH";
    private const string ExeName = "Ocrx.Engine.exe";
    private static readonly string InstalledPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OcrxEngine",
        "current",
        ExeName);

    /// <summary>
    /// The env var, if set — CI pins this to the exact artifact it downloaded. Otherwise uses the
    /// current per-user OCRX installation. The SDK never probes engine source-build directories:
    /// the engine is a separate repository and replay parity must exercise a released binary.
    /// </summary>
    public static string Resolve() =>
        Environment.GetEnvironmentVariable(EnvVar) is { Length: > 0 } fromEnv
            ? File.Exists(fromEnv)
                ? fromEnv
                : throw new InvalidOperationException($"{EnvVar} is set to '{fromEnv}', but no file exists there.")
            : File.Exists(InstalledPath)
                ? InstalledPath
                : throw new InvalidOperationException(
                    $"Could not find {ExeName}. Set {EnvVar} to a released engine executable or install OCRX for the current user.");
}
