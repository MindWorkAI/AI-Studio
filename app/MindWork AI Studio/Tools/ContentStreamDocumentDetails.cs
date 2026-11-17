using System.Text.Json.Serialization;

namespace AIStudio.Tools;

public sealed class ContentStreamDocumentDetails
{
    [JsonPropertyName("page_number")]
    public int? PageNumber { get; set; }

    /// <summary>
    /// Whether the page is one the file itself knows, such as a page of a OneNote section, rather
    /// than one the runtime estimated from the page breaks of a Word or OpenDocument file.
    /// </summary>
    [JsonPropertyName("page_is_exact")]
    public bool PageIsExact { get; set; }

    [JsonPropertyName("image")]
    public ContentStreamPptxImageData? Image { get; set; }
}
