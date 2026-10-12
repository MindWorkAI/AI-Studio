using System.Text.Json;

namespace AIStudio.Chat;

/// <summary>
/// Structured data which a renderer may use instead of the canonical text of a message.
/// </summary>
public sealed class ContentPresentation
{
    /// <summary>
    /// The renderer which understands the data.
    /// </summary>
    public ContentPresentationKind Kind { get; set; } = ContentPresentationKind.NONE;

    /// <summary>
    /// The renderer-specific data.
    /// </summary>
    public JsonElement? Data { get; set; }

    internal static ContentPresentation Create<T>(ContentPresentationKind kind, T data) => new()
    {
        Kind = kind,
        Data = JsonSerializer.SerializeToElement(data),
    };

    internal ContentPresentation DeepClone() => new()
    {
        Kind = this.Kind,
        Data = this.Data?.Clone(),
    };

    internal int GetDataHashCode() => this.Data is not { } data || data.ValueKind is JsonValueKind.Undefined
        ? 0
        : data.GetRawText().GetHashCode(StringComparison.Ordinal);
}