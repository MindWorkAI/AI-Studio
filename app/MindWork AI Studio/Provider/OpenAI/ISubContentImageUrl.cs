namespace AIStudio.Provider.OpenAI;

/// <summary>
/// Contract for nested image URL sub-content.
/// </summary>
/// <remarks>
/// Some providers use a nested object format for image URLs:
/// <code>
/// { "type": "image_url", "image_url": { "url": "data:image/jpeg;base64,..." } }
/// </code>
/// This interface represents the inner object with the "url" property.
/// </remarks>
public interface ISubContentImageUrl 
{
    /// <summary>
    /// The URL or base64-encoded data URI of the image.
    /// </summary>
    public string Url { get; init; }
}
