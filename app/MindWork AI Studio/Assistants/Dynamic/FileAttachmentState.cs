using AIStudio.Chat;

namespace AIStudio.Assistants.Dynamic;

public sealed class FileAttachmentState
{
    public HashSet<FileAttachment> DocumentPaths { get; set; } = [];

    /// <summary>
    /// Creates a copy which shares nothing with this state.
    /// </summary>
    /// <returns>The copy.</returns>
    public FileAttachmentState Copy() => new() { DocumentPaths = new HashSet<FileAttachment>(this.DocumentPaths, this.DocumentPaths.Comparer) };

    /// <summary>
    /// Tells whether another state holds the same files as this one.
    /// </summary>
    /// <param name="other">The state to compare with.</param>
    /// <returns><c>true</c> when both states hold the same files; otherwise, <c>false</c>.</returns>
    public bool HasSameInputsAs(FileAttachmentState other) => this.DocumentPaths.SetEquals(other.DocumentPaths);
}
