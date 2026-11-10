using System.Diagnostics.CodeAnalysis;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIStudio.Tools.DocumentSegments;

/// <summary>
/// Writes the segments of a batch for a model and reads them back out of its answer.
/// </summary>
internal static class SegmentJson
{
    /// <summary>
    /// The options with which the segments go into a prompt.
    /// </summary>
    /// <remarks>
    /// The relaxed encoder writes umlauts and the angle brackets of the tags as they are, rather than as
    /// escapes which a model might copy into its answer. Nothing embeds this JSON into HTML.
    /// </remarks>
    internal static readonly JsonSerializerOptions PROMPT_OPTIONS = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        TypeInfoResolver = SegmentJsonContext.Default,
    }.AsReadOnly();

    /// <summary>
    /// The options with which the answer of a model is read, forgiving what models like to write.
    /// </summary>
    internal static readonly JsonSerializerOptions ANSWER_OPTIONS = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        RespectNullableAnnotations = true,
        TypeInfoResolver = SegmentJsonContext.Default,
    }.AsReadOnly();

    /// <summary>
    /// Writes a batch for a prompt.
    /// </summary>
    /// <param name="batch">The segments.</param>
    public static string Write(SegmentBatch batch) => JsonSerializer.Serialize(batch, PROMPT_OPTIONS);

    /// <summary>
    /// Reads the batch out of the answer of a model, without checking its content.
    /// </summary>
    /// <param name="answer">The answer of the model.</param>
    /// <param name="batch">The batch, or null when reading failed.</param>
    /// <param name="issue">Why reading failed, written for the model; empty on success.</param>
    /// <param name="breaksOff">Whether the answer starts a JSON object but ends before it is complete, e.g., because the model reached its limit of tokens.</param>
    /// <returns>Whether the answer holds a batch which could be read.</returns>
    public static bool TryRead(string answer, [NotNullWhen(true)] out SegmentBatch? batch, out string issue, out bool breaksOff)
    {
        batch = null;
        issue = string.Empty;
        breaksOff = false;

        var json = ModelAnswerJson.ExtractObject(answer);
        if (json.IsWhiteSpace())
        {
            breaksOff = answer.Contains('{');
            issue = "The answer contains no complete JSON object.";
            return false;
        }

        try
        {
            batch = JsonSerializer.Deserialize<SegmentBatch>(json, ANSWER_OPTIONS);
        }
        catch (JsonException e)
        {
            issue = $"The JSON object cannot be read: {e.Message}";
            return false;
        }

        if (batch is null)
        {
            issue = "The JSON object is empty.";
            return false;
        }

        return true;
    }
}