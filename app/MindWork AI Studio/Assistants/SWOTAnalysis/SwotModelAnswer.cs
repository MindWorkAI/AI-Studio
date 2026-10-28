using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace AIStudio.Assistants.SWOTAnalysis;

/// <summary>
/// Reads the JSON object out of the answer of a model which works on the SWOT analysis.
/// </summary>
internal static class SwotModelAnswer
{
    private static readonly JsonSerializerOptions JSON_OPTIONS = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        RespectNullableAnnotations = true,
    };

    /// <summary>
    /// Reads the JSON object of a model answer into the given type, without checking its content.
    /// </summary>
    /// <remarks>
    /// A null where the type expects a value fails the reading, and the issue names the field. A null
    /// inside a list, though, is read as such, since the annotations of list items do not count.
    /// </remarks>
    /// <param name="answer">The answer of the model.</param>
    /// <param name="value">The object, or null when reading failed.</param>
    /// <param name="issue">Why reading failed, written for the model; empty on success.</param>
    /// <returns>Whether the answer holds an object which could be read.</returns>
    public static bool TryDeserialize<T>(string answer, [NotNullWhen(true)] out T? value, out string issue) where T : class
    {
        value = null;
        issue = string.Empty;

        var json = ModelAnswerJson.ExtractObject(answer);
        if (json.IsWhiteSpace())
        {
            issue = "The answer contains no complete JSON object.";
            return false;
        }

        try
        {
            value = JsonSerializer.Deserialize<T>(json, JSON_OPTIONS);
        }
        catch (JsonException e)
        {
            issue = $"The JSON object cannot be read: {e.Message}";
            return false;
        }

        if (value is null)
        {
            issue = "The JSON object is empty.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Reads an answer, and asks the model once more when the answer cannot be read.
    /// </summary>
    /// <remarks>
    /// The caller sends the repair request into the same thread, so the model sees its own answer next
    /// to what is wrong with it. Exactly one round, as in the Slide Assistant: a model which misses the
    /// form twice in a row seldom hits it the third time, and every round costs as much as the first.
    /// </remarks>
    /// <param name="answer">The first answer of the model.</param>
    /// <param name="read">Reads an answer and names what is wrong with it.</param>
    /// <param name="askAgain">Gets the issue of the first answer, sends RepairRequestOf it, and returns the new answer; an empty one when the provider failed.</param>
    /// <returns>The result of the first answer which could be read, or the issue of the last one.</returns>
    public static async Task<SwotReadAnswer<TResult>> ReadWithOneRepairAsync<TResult>(string answer, SwotAnswerReader<TResult> read, Func<string, Task<string>> askAgain) where TResult : class
    {
        if (read(answer, out var result, out var issue))
            return new(result, string.Empty);

        var repairedAnswer = await askAgain(issue);
        if (string.IsNullOrWhiteSpace(repairedAnswer))
            return new(null, issue);

        return read(repairedAnswer, out result, out issue) ? new(result, string.Empty) : new(null, issue);
    }

    /// <summary>
    /// The request which asks a model for its answer once more, in the expected form.
    /// </summary>
    /// <param name="issue">What is wrong with the answer, as the parser named it.</param>
    public static string RepairRequestOf(string issue) =>
        $"""
         Your answer cannot be used yet: {issue}

         Answer with the complete corrected JSON object and nothing else.
         """;

    /// <summary>
    /// Whether an item of a list read from a model answer is null.
    /// </summary>
    /// <remarks>
    /// For removing the nulls a model wrote into a list: the annotations of list items do not count
    /// when reading, so a list may hold a null although its type says otherwise.
    /// </remarks>
    /// <param name="item">The item of the list.</param>
    /// <returns>Whether the item is null.</returns>
    public static bool IsNull(object? item) => item is null;
}