using SharedTools;

namespace AIStudio.Settings;

/// <summary>
/// The tokenizer path an exported provider carries, which a configuration plugin and an import both read.
/// </summary>
/// <remarks>
/// AI Studio keeps the tokenizer of a provider as a copy below its data directory, in a folder of the
/// provider's own, e.g., tokenizers/chat_[ID]/tokenizer.json. An absolute path to that copy is of no use
/// in an export: a configuration plugin reads the path relative to its own folder and ignores one which
/// leads out of it, and the path would give away the user name of whoever exported it.<br/><br/>
///
/// The export therefore names the copy relative to the data directory, and one string serves both
/// readers. In a configuration plugin, it is relative to the folder of the plugin, where the
/// administrator copies the file to the same place. Imported on the computer it came from, it is
/// relative to the data directory and names the copy itself.
/// </remarks>
public static class ExportedTokenizerPath
{
    private const string TOKENIZERS_DIRECTORY = "tokenizers";

    /// <summary>
    /// Turns the path of the tokenizer copy of a provider into the path an export carries.
    /// </summary>
    /// <remarks>
    /// The runtime stores the copy as tokenizers/[model ID]/[file name] below the data directory, so this
    /// builds the same path from the model ID instead of asking where the data directory lies. The file
    /// name is taken after the last separator of either kind, which gives the same result on every OS,
    /// also for a path written on another one.
    /// </remarks>
    /// <param name="tokenizerPath">The path of the tokenizer copy, as the provider holds it in memory.</param>
    /// <param name="tokenizerModelId">The model the tokenizer belongs to, as TokenizerModelId builds it.</param>
    /// <returns>The path relative to the data directory, or an empty string when the provider has no tokenizer.</returns>
    public static string FromLocal(string tokenizerPath, string tokenizerModelId)
    {
        if (string.IsNullOrWhiteSpace(tokenizerPath))
            return string.Empty;

        var fileName = tokenizerPath[(tokenizerPath.LastIndexOfAny(['/', '\\']) + 1)..];
        return string.IsNullOrWhiteSpace(fileName) ? string.Empty : $"{TOKENIZERS_DIRECTORY}/{tokenizerModelId}/{fileName}";
    }

    /// <summary>
    /// Turns the tokenizer path of an imported export into a path on this computer.
    /// </summary>
    /// <remarks>
    /// A relative path names a copy below the data directory, and only one inside its folder of
    /// tokenizers: an export comes from somebody else, so it gets no way to name any other file of the
    /// data directory. An absolute path stems from an export of an older version and is taken as it is.
    /// Whether the file exists is up to the caller.
    /// </remarks>
    /// <param name="exportedPath">The tokenizer path as the export carries it.</param>
    /// <param name="dataDirectory">The data directory, or null while it is not known yet.</param>
    /// <returns>The path on this computer, or an empty string when the export names no tokenizer copy below the data directory.</returns>
    public static string ToLocal(string exportedPath, string? dataDirectory)
    {
        if (string.IsNullOrWhiteSpace(exportedPath) || Path.IsPathFullyQualified(exportedPath))
            return exportedPath;

        if (string.IsNullOrWhiteSpace(dataDirectory))
            return string.Empty;

        var path = DataDirectoryRelativePathConverter.ToAbsolute(exportedPath, dataDirectory);
        return !string.IsNullOrWhiteSpace(path) && PathTools.IsInsideDirectory(Path.Join(dataDirectory, TOKENIZERS_DIRECTORY), path) ? path : string.Empty;
    }

    /// <summary>
    /// Writes the TokenizerPath field of an exported provider.
    /// </summary>
    /// <remarks>
    /// The file itself does not travel with the Lua code, so a field naming one brings a note on where it
    /// belongs. The lines after the first carry the indentation, so the field fits wherever its first
    /// line is placed.
    /// </remarks>
    /// <param name="tokenizerPath">The path of the tokenizer copy, as the provider holds it in memory.</param>
    /// <param name="tokenizerModelId">The model the tokenizer belongs to, as TokenizerModelId builds it.</param>
    /// <param name="indentation">The indentation of the field within its table.</param>
    /// <returns>The Lua code of the field, ending with a comma.</returns>
    public static string BuildLuaField(string tokenizerPath, string tokenizerModelId, string indentation)
    {
        var exportedPath = FromLocal(tokenizerPath, tokenizerModelId);
        var field = $"""["TokenizerPath"] = "{LuaTools.EscapeLuaString(exportedPath)}",""";
        if (string.IsNullOrEmpty(exportedPath))
            return field;

        string[] lines =
        [
            "-- This provider uses a tokenizer of its own. Ship the file with this plugin:",
            "-- AI Studio keeps it in its data directory, below the path named here. Copy",
            "-- it to the same place in the folder of this plugin, or change the path to",
            "-- where you put it. Without the file, AI Studio uses its default tokenizer.",
            field,
        ];

        return string.Join($"{Environment.NewLine}{indentation}", lines);
    }
}