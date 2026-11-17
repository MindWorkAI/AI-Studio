using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// The JSON options of the files of the BeyondFacts Lab.
/// </summary>
internal static class BeyondFactsLabJson
{
    /// <summary>
    /// The options with which the lab writes and reads its files.
    /// </summary>
    /// <remarks>
    /// The files stay readable for people who look into the data folder: indented, enums by name, and umlauts
    /// as they are rather than as escapes. The relaxed encoder is fine here, because nothing embeds these files
    /// into HTML. A field this version does not know makes a file unreadable rather than letting a save drop
    /// it; such a preparation is listed as one of a newer version or as unavailable, and nothing writes it.
    /// </remarks>
    internal static readonly JsonSerializerOptions PERSISTENCE = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter() },
        TypeInfoResolver = BeyondFactsLabJsonContext.Default,
    }.AsReadOnly();
}