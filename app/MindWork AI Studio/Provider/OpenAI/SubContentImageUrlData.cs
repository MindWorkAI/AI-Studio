namespace AIStudio.Provider.OpenAI;

/// <summary>
/// Represents the inner object of a nested image URL sub-content.
/// </summary>
/// <remarks>
/// This record is used when the provider expects the format:
/// <code>
/// { "type": "image_url", "image_url": { "url": "data:image/jpeg;base64,..." } }
/// </code>
/// This class represents the inner <c>{ "url": "..." }</c> part.
/// </remarks>
public record SubContentImageUrlData : ISubContentImageUrl
{
    /// <inheritdoc />
    public string Url { get; init; } = string.Empty;
}
