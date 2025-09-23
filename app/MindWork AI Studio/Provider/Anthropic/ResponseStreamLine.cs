// ReSharper disable NotAccessedPositionalProperty.Global
namespace AIStudio.Provider.Anthropic;

/// <summary>
/// Represents a response stream line.
/// </summary>
/// <param name="Type">The type of the response line.</param>
/// <param name="Index">The index of the response line.</param>
/// <param name="Delta">The delta of the response line.</param>
public readonly record struct ResponseStreamLine(string Type, int Index, Delta Delta) : IResponseStreamLine
{
    /// <inheritdoc />
    public bool ContainsContent() => this != default && !string.IsNullOrWhiteSpace(this.Delta.Text);

    /// <inheritdoc />
    public ContentStreamChunk GetContent() => new(this.Delta.Text, []);

    #region Implementation of IAnnotationStreamLine

    //
    // Please note: Anthropic's API does not currently support sources in their
    // OpenAI-compatible response stream.
    //

    /// <inheritdoc />
    public bool ContainsSources() => false;

    /// <inheritdoc />
    public IList<ISource> GetSources() => [];

    #endregion
}

/// <summary>
/// The delta object of a response line.
/// </summary>
/// <param name="Type">The type of the delta.</param>
/// <param name="Text">The text of the delta.</param>
public readonly record struct Delta(string Type, string Text);