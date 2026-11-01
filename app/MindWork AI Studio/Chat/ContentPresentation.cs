using System.Text.Json;

namespace AIStudio.Chat;

/// <summary>
/// Structured data which a renderer may use instead of the canonical text of a message.
/// </summary>
/// <remarks>
/// The text stays canonical: providers, copying, exporting, and every renderer which does not know
/// the presentation use it. ContentPresentationView shows the presentation instead, and falls back to
/// the text when its data cannot be read, e.g., from a chat file which was changed by hand.
/// </remarks>
public sealed class ContentPresentation
{
    private JsonElement? data;
    private int? dataHashCode;

    /// <summary>
    /// The renderer which understands the data.
    /// </summary>
    public ContentPresentationKind Kind { get; set; } = ContentPresentationKind.NONE;

    /// <summary>
    /// The renderer-specific data.
    /// </summary>
    public JsonElement? Data
    {
        get => this.data;
        set
        {
            this.data = value;
            this.dataHashCode = null;
        }
    }

    internal static ContentPresentation Create<T>(ContentPresentationKind kind, T data, JsonSerializerOptions options) => new()
    {
        Kind = kind,
        Data = JsonSerializer.SerializeToElement(data, options),
    };

    internal ContentPresentation DeepClone() => new()
    {
        Kind = this.Kind,
        Data = this.Data?.Clone(),
    };

    /// <summary>
    /// A hash code of the data, for telling whether it changed.
    /// </summary>
    /// <remarks>
    /// Computed once per data, since every render of a message asks for it and computing means
    /// writing out the whole data.
    /// </remarks>
    internal int GetDataHashCode() => this.dataHashCode ??= this.Data is not { ValueKind: not JsonValueKind.Undefined } value
        ? 0
        : value.GetRawText().GetHashCode(StringComparison.Ordinal);
}