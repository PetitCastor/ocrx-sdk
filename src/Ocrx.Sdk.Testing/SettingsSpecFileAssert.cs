namespace Ocrx.Sdk.Testing;

/// <summary>Assertions plugin test suites use to keep a shipped settings file in lockstep with code.</summary>
public static class SettingsSpecFileAssert
{
    /// <summary>Throws when the schema in <paramref name="path"/> differs from <paramref name="expected"/>.</summary>
    public static void Matches(SettingsSpec expected, string path)
    {
        ArgumentNullException.ThrowIfNull(expected);
        if (!SettingsSpecFile.TryRead(path, out var actual)
            || System.Text.Json.JsonSerializer.Serialize(actual) != System.Text.Json.JsonSerializer.Serialize(expected))
            throw new InvalidOperationException($"Shipped settings spec '{path}' does not match the runtime schema.");
    }
}
