using System.Text.Json;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// How the BeyondFacts Lab reads the JSON which models write for it.
/// </summary>
internal static class BflModelAnswerJson
{
    /// <summary>
    /// The options for the JSON in a model answer. They forgive what models often write, such as trailing commas,
    /// comments, or another case, but refuse a field named twice: another reader might see its other value.
    /// </summary>
    internal static readonly JsonSerializerOptions OPTIONS = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        RespectNullableAnnotations = true,
        AllowDuplicateProperties = false,
        TypeInfoResolver = BflModelAnswerJsonContext.Default,
    }.AsReadOnly();
}