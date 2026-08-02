using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Provides the single JSON configuration used by visual briefing persistence and hashing.
/// </summary>
internal static class VisualBriefingJson
{
    /// <summary>
    /// Gets compact canonical JSON options.
    /// </summary>
    internal static JsonSerializerOptions Compact { get; } = Create(writeIndented: false);

    /// <summary>
    /// Gets indented persistence JSON options.
    /// </summary>
    internal static JsonSerializerOptions Indented { get; } = Create(writeIndented: true);

    /// <summary>
    /// Creates the shared JSON configuration.
    /// </summary>
    /// <remarks>
    /// Enums are written as their member names instead of numbers. Stored briefings outlive many
    /// releases, so a numeric value would silently change meaning as soon as somebody inserts or
    /// reorders an enum member. Most visual briefing enums carry the converter as an attribute
    /// already; this option covers the ones defined outside the feature, such as the target language
    /// and the audience enums. Reading still accepts numbers, so briefings written before this
    /// change keep loading.
    /// </remarks>
    /// <param name="writeIndented">Whether serialized JSON should be indented.</param>
    /// <returns>The configured serializer options.</returns>
    private static JsonSerializerOptions Create(bool writeIndented) => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = writeIndented,
        Encoder = JavaScriptEncoder.Default,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter() },
    };
}
