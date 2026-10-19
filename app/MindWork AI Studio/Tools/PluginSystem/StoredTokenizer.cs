namespace AIStudio.Tools.PluginSystem;

/// <summary>
/// The tokenizer a provider of a configuration plugin points to: the copy stored below the data
/// directory, along with the fingerprint of its content.
/// </summary>
/// <param name="Path">The stored copy, or an empty string when no tokenizer is stored.</param>
/// <param name="Fingerprint">The fingerprint of the stored content, or an empty string when no file could be read.</param>
internal readonly record struct StoredTokenizer(string Path, string Fingerprint)
{
    /// <summary>
    /// No tokenizer is stored.
    /// </summary>
    public static readonly StoredTokenizer NONE = new(string.Empty, string.Empty);
}