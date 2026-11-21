using System.Text.Json;

namespace AIStudio.AIOperations;

/// <summary>
/// How AI operations read the JSON in the answers of models.
/// </summary>
/// <remarks>
/// Find the JSON in an answer with ModelAnswerJson first, then read it with these options.
/// </remarks>
internal static class AIOperationsJson
{
    /// <summary>
    /// The options for the JSON in a model answer to an AI operation.
    /// </summary>
    internal static readonly JsonSerializerOptions OPTIONS = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        TypeInfoResolver = AIOperationsJsonContext.Default,
    }.AsReadOnly();
}
