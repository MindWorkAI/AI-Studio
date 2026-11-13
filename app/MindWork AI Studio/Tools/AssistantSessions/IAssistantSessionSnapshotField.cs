namespace AIStudio.Tools.AssistantSessions;

/// <summary>
/// Provides a typed value stored in an assistant session snapshot.
/// </summary>
public interface IAssistantSessionSnapshotField
{
    /// <summary>
    /// Gets the type used when the value was captured.
    /// </summary>
    Type ValueType { get; }

    /// <summary>
    /// Tries to read the captured value as the requested type.
    /// </summary>
    /// <typeparam name="T">The requested value type.</typeparam>
    /// <param name="value">The typed value when reading succeeds.</param>
    /// <returns><c>true</c> when the captured value matches <typeparamref name="T"/>; otherwise, <c>false</c>.</returns>
    bool TryRead<T>(out T? value);

    /// <summary>
    /// Tells whether another field provably holds the same value as this one.
    /// </summary>
    /// <remarks>
    /// The same means: the same captured type, and the same reference, an equal string or value of a
    /// value type, or a collection whose items are the same by this rule in turn. Everything else counts
    /// as changed. A field wrongly taken for changed is only restored once more, while a field wrongly
    /// taken for the same would lose an update.
    /// </remarks>
    /// <param name="other">The field to compare with.</param>
    /// <returns><c>true</c> when both fields provably hold the same value; otherwise, <c>false</c>.</returns>
    bool HasSameValueAs(IAssistantSessionSnapshotField other);
}