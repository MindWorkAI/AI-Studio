namespace AIStudio.Provider;

/// <summary>
/// The API key for loading the models of a provider, ready for a header.
/// </summary>
/// <remarks>
/// A plain struct instead of a record on purpose: the generated ToString of a record would print the
/// key wherever somebody logs this value by mistake.
/// </remarks>
/// <param name="key">The key without the whitespace around it, or null when there is none or it cannot be sent.</param>
/// <param name="isUnusable">Whether the key contains a character which an HTTP header cannot carry.</param>
public readonly struct ModelLoadingSecretKey(string? key, bool isUnusable)
{
    /// <summary>
    /// No key at all, e.g., because none is stored.
    /// </summary>
    public static readonly ModelLoadingSecretKey NONE = new(null, false);

    /// <summary>
    /// A key which cannot be sent, and which therefore is not handed out.
    /// </summary>
    public static readonly ModelLoadingSecretKey UNUSABLE = new(null, true);

    /// <summary>
    /// The key without the whitespace around it, or null when there is none or it cannot be sent.
    /// </summary>
    public string? Key { get; } = key;

    /// <summary>
    /// Whether the key contains a character which an HTTP header cannot carry.
    /// </summary>
    public bool IsUnusable { get; } = isUnusable;
}