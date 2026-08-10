using System.Text.Json.Serialization;

namespace AIStudio.Tools;

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable ClassNeverInstantiated.Global
public sealed class ContentStreamErrorDetails
{
    [JsonPropertyName("code")]
    public string? Code { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    /// <summary>
    /// The page the failure belongs to, when the failure affects a single page only.
    /// </summary>
    [JsonPropertyName("page_number")]
    public int? PageNumber { get; init; }

    /// <summary>
    /// Gets the parsed error code.
    /// </summary>
    /// <remarks>
    /// Codes this version does not know map to <see cref="FileExtractionErrorCode.UNKNOWN"/>
    /// instead of failing the deserialization. A failed deserialization would turn the reported
    /// error back into empty file content, which is exactly what we want to avoid here.
    /// </remarks>
    [JsonIgnore]
    public FileExtractionErrorCode ParsedCode => Enum.TryParse<FileExtractionErrorCode>(this.Code, ignoreCase: true, out var parsedCode) ? parsedCode : FileExtractionErrorCode.UNKNOWN;

    /// <summary>
    /// Gets a value indicating whether this failure affects one part of the file only, while the
    /// remaining content is still usable.
    /// </summary>
    [JsonIgnore]
    public bool IsPartialFailure => this.ParsedCode is FileExtractionErrorCode.PAGE_EXTRACTION_FAILED;
}