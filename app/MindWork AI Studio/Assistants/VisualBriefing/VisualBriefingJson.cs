using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Provides the two JSON configurations used by visual briefing hashing and persistence.
/// </summary>
/// <remarks>
/// The split is deliberate and the two halves must not be merged back together. Hashing needs bytes
/// that never change, persistence wants output that stays readable as the app evolves. One shared
/// configuration cannot serve both: improving the readability of stored files would rewrite the very
/// bytes that older briefings were hashed with, and every one of them would fail its integrity check.
/// For the same reason both configurations are written out in full instead of sharing a factory, which
/// is what <see cref="CanonicalJsonConfigurationAttribute"/> and the rule MWAIS0010 enforce: a shared
/// factory lets a change intended for the persistence side reach the hashed side unnoticed.
/// </remarks>
internal static class VisualBriefingJson
{
    /// <summary>
    /// Gets the frozen options whose byte output is hashed into stored briefings.
    /// </summary>
    /// <remarks>
    /// Treat these options as frozen. Their exact bytes are hashed into stored briefings: the artifact
    /// header is serialized into the briefing document, and reading that document back re-serializes the
    /// header to recompute the document hash. Every build stage likewise hashes its serialized output,
    /// and a mismatch makes the store discard the stored artifact. Any change here — a converter, a
    /// naming policy, an encoder — therefore invalidates every briefing that was ever written, which
    /// surfaces as a failed integrity check rather than as a build error. This is why enums stay numeric
    /// here even though the persisted manifest writes their member names.
    /// </remarks>
    [CanonicalJsonConfiguration]
    internal static JsonSerializerOptions Canonical { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = false,
        Encoder = JavaScriptEncoder.Default,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    /// <summary>
    /// Gets the options for files that are read back by name rather than by hash.
    /// </summary>
    /// <remarks>
    /// These options are free to evolve, because nothing hashes their output. They write the briefing
    /// manifest and the diagnostics clipboard text, where readable enum names are worth having: stored
    /// briefings outlive many releases, so a numeric value would silently change meaning as soon as
    /// somebody inserts or reorders an enum member. Most visual briefing enums carry the converter as an
    /// attribute already, which applies to both configurations; the converter below only covers the ones
    /// defined outside the feature, such as the target language and the audience enums. Reading accepts
    /// numbers as well, so manifests written before this distinction existed keep loading.
    /// </remarks>
    internal static JsonSerializerOptions Persistence { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Default,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter() },
    };
}