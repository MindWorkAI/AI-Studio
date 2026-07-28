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
    /// <param name="writeIndented">Whether serialized JSON should be indented.</param>
    /// <returns>The configured serializer options.</returns>
    private static JsonSerializerOptions Create(bool writeIndented) => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = writeIndented,
        Encoder = JavaScriptEncoder.Default,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };
}
