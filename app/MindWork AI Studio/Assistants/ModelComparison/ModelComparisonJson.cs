using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// The JSON options of the files of the Model Comparison assistant.
/// </summary>
internal static class ModelComparisonJson
{
    /// <summary>
    /// The options with which the assistant writes and reads its files.
    /// </summary>
    /// <remarks>
    /// The files stay readable for people who look into the data folder: indented, enums by name, and umlauts as they
    /// are rather than as escapes. The relaxed encoder is fine here, because nothing embeds these files into HTML. A
    /// field this version does not know makes a file unreadable rather than letting a save drop it; the store then
    /// leaves the file alone.
    /// </remarks>
    internal static readonly JsonSerializerOptions PERSISTENCE = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter() },
        TypeInfoResolver = ModelComparisonJsonContext.Default,
    }.AsReadOnly();
}