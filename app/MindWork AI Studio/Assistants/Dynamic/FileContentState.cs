namespace AIStudio.Assistants.Dynamic;

public sealed class FileContentState
{
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Creates a copy which shares nothing with this state.
    /// </summary>
    /// <returns>The copy.</returns>
    public FileContentState Copy() => new() { Content = this.Content };

    /// <summary>
    /// Tells whether another state holds the same content as this one.
    /// </summary>
    /// <param name="other">The state to compare with.</param>
    /// <returns><c>true</c> when both states hold the same content; otherwise, <c>false</c>.</returns>
    public bool HasSameInputsAs(FileContentState other) => string.Equals(this.Content, other.Content, StringComparison.Ordinal);
}
