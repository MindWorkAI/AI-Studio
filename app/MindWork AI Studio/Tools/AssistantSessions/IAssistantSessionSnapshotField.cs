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
}