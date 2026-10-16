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
    /// The format the runtime identified by looking at the content, e.g. when it contradicts the
    /// file extension.
    /// </summary>
    [JsonPropertyName("detected_format")]
    public string? DetectedFormat { get; init; }

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

    /// <summary>
    /// Gets a value indicating whether this is a notice rather than a failure.
    /// </summary>
    /// <remarks>
    /// A notice tells the user something worth knowing about the file, while the content itself
    /// was read completely. It must therefore never degrade the outcome of an extraction.
    /// </remarks>
    [JsonIgnore]
    public bool IsNotice => this.ParsedCode is FileExtractionErrorCode.EXTENSION_MISMATCH;
}