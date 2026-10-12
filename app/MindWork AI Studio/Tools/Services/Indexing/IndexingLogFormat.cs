namespace AIStudio.Tools.Services.Indexing;

/// <summary>
/// How the indexing writes values into the log.
/// </summary>
internal static class IndexingLogFormat
{
    /// <summary>
    /// Shortens a hash or a signature to what is needed to tell two of them apart in the log.
    /// </summary>
    /// <param name="value">The hash or signature.</param>
    /// <returns>Its first twelve characters, or a marker when it is empty.</returns>
    public static string ShortHash(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "<empty>";

        return value.Length <= 12 ? value : value[..12];
    }
}