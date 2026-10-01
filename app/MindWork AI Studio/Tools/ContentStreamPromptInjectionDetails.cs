using System.Text.Json.Serialization;
using AIStudio.Tools.Security;

namespace AIStudio.Tools;

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable ClassNeverInstantiated.Global

/// <summary>
/// Reports that the runtime filtered suspected prompt injections out of a file.
/// </summary>
/// <remarks>
/// This is a notice, not a failure: the file was read and everything around the filtered
/// passages is intact. It travels beside the content rather than as an error code, because the
/// app needs the findings themselves to tell the user what was removed.
/// </remarks>
public sealed class ContentStreamPromptInjectionDetails
{
    [JsonPropertyName("findings")]
    public List<PromptInjectionFinding>? Findings { get; init; }

    /// <summary>
    /// How many passages were filtered. Can exceed the number of findings, because the runtime
    /// caps how many it reports in detail while it filters every single one.
    /// </summary>
    [JsonPropertyName("redacted_count")]
    public int RedactedCount { get; init; }
}