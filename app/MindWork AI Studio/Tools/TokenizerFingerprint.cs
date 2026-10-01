using System.Security.Cryptography;

namespace AIStudio.Tools;

/// <summary>
/// Identifies a tokenizer by what is inside its file, not by where the file lies.
/// </summary>
/// <remarks>
/// The embedding signature asks this to decide whether stored vectors still belong to the current
/// configuration, and the path cannot answer it. A tokenizer is stored below the data directory under
/// the model it belongs to, keeping the name it came with -- and the usual name for one is
/// tokenizer.json. Picking a different tokenizer with that name lands on the identical path, so the
/// index would be kept although the chunk boundaries moved. The other way round, moving the data
/// directory changes every path without changing a single tokenizer.
/// </remarks>
public static class TokenizerFingerprint
{
    /// <summary>
    /// Reads a tokenizer file and returns a fingerprint of its content.
    /// </summary>
    /// <param name="tokenizerPath">The tokenizer file to read. May be empty when no tokenizer is set.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The fingerprint, or an empty string when there is no readable file.</returns>
    public static async Task<string> ForFileAsync(string tokenizerPath, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(tokenizerPath))
            return string.Empty;

        try
        {
            await using var stream = File.OpenRead(tokenizerPath);
            return Convert.ToHexString(await SHA256.HashDataAsync(stream, token));
        }
        catch
        {
            //
            // An unreadable tokenizer is not this method's problem to report: the dialog validates the
            // file before it ever gets here, and an indexing run says so again when it cannot tokenize
            // anything. Whoever stores a provider has to decide what an empty answer means for them,
            // because writing it into the settings would look like another tokenizer and throw the
            // stored vectors away.
            //
            return string.Empty;
        }
    }
}