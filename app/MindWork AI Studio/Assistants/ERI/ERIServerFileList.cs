using System.Text.Json;

namespace AIStudio.Assistants.ERI;

/// <summary>
/// The files which the model of the ERI Server assistant wants to create.
/// </summary>
/// <remarks>
/// The model answers with a JSON object of the form {"files": ["path/to/file1", "path/to/file2"]}. Up to
/// v26.10.1, the assistant asked for invalid JSON, an object holding a bare list, and took every string of
/// the answer for a file, apart from the word "files". A path with a space was lost, while any other key
/// the model came up with became a file.
/// </remarks>
internal sealed class ERIServerFileList
{
    private static readonly JsonSerializerOptions JSON_OPTIONS = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = 8,
    };

    /// <summary>
    /// The paths of the files, relative to the base directory.
    /// </summary>
    public List<string>? Files { get; init; }

    /// <summary>
    /// Reads the list of files out of the answer of the model.
    /// </summary>
    /// <param name="answer">The answer of the model to the request for the list of files.</param>
    /// <param name="files">The paths of the files, without empty ones, or an empty list when the answer
    /// holds none.</param>
    /// <returns>True, when the answer holds a list with at least one file.</returns>
    public static bool TryParse(string answer, out IReadOnlyList<string> files)
    {
        files = [];
        var json = ModelAnswerJson.ExtractObject(answer);
        if (json.IsWhiteSpace())
            return false;

        ERIServerFileList? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<ERIServerFileList>(json, JSON_OPTIONS);
        }
        catch (JsonException)
        {
            return false;
        }

        var paths = parsed?.Files?.Where(file => !string.IsNullOrWhiteSpace(file)).ToList() ?? [];
        if (paths.Count is 0)
            return false;

        files = paths;
        return true;
    }
}