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
    public string GetContent() => this.Delta.Text;
}

/// <summary>
/// The delta object of a response line.
/// </summary>
/// <param name="Type">The type of the delta.</param>
/// <param name="Text">The text of the delta.</param>
public readonly record struct Delta(string Type, string Text);