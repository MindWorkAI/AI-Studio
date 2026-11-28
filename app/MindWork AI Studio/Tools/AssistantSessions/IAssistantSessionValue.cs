namespace AIStudio.Tools.AssistantSessions;

/// <summary>
/// A value of an assistant session which tells by itself whether another value holds the same.
/// </summary>
/// <remarks>
/// A snapshot compares most values by their reference, since an object may change without getting a new
/// one, see IAssistantSessionSnapshotField.HasSameValueAs. A value which the assistant captures as a copy
/// of its own, such as the state of an assistant plugin, gets a new reference with every snapshot, though,
/// and would always count as changed. Such a value compares its content instead.
/// </remarks>
public interface IAssistantSessionValue
{
    /// <summary>
    /// Tells whether another value holds the same content as this one.
    /// </summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns><c>true</c> when both values hold the same content; otherwise, <c>false</c>.</returns>
    bool HasSameValueAs(object other);
}