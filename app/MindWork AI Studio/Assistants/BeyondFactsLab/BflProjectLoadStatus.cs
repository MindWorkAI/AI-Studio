namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Whether this version of AI Studio can open a stored preparation of the BeyondFacts Lab.
/// </summary>
public enum BflProjectLoadStatus
{
    /// <summary>
    /// The preparation can be opened.
    /// </summary>
    AVAILABLE,

    /// <summary>
    /// A newer version of AI Studio stored the preparation. It stays as it is until that version opens it again.
    /// </summary>
    NEWER_VERSION,

    /// <summary>
    /// The preparation cannot be read, e.g., because its file is damaged. It stays as it is; nothing deletes it on its own.
    /// </summary>
    UNAVAILABLE,
}