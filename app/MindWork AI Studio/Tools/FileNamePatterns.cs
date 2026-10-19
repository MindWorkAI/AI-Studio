using System.IO.Enumeration;

using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Rust;

namespace AIStudio.Tools;

/// <summary>
/// Parses and matches the semicolon-separated file-name patterns and semantic selectors used by
/// folder-based features.
/// </summary>
internal static class FileNamePatterns
{
    /// <summary>
    /// A semantic pattern for every document AI Studio can read: the known document types, such as
    /// PDF, Office files, and source code, plus every other file whose contents can be read as text.
    /// Unlike <c>*</c>, it does not select media or binary files.
    /// </summary>
    public const string ALL_DOCUMENTS_PATTERN = ":documents:";

    /// <summary>
    /// A semantic pattern for every file whose contents can be read as text, including files
    /// without a known extension. Unlike <c>*</c>, it does not select media or binary documents.
    /// </summary>
    public const string ALL_TEXT_FILES_PATTERN = ":text:";

    /// <summary>
    /// What every selector begins and ends with, so that a mistyped selector can be told apart from
    /// a file name pattern.
    /// </summary>
    private const char SELECTOR_DELIMITER = ':';

    private enum ValidationIssue
    {
        NONE,
        MISSING,
        EMPTY_ENTRY,
        UNKNOWN_SELECTOR,
        RECURSIVE_WILDCARD,
        PATH,
        INVALID_CHARACTER,
    }

    /// <summary>
    /// What a file name pattern must not contain: what no file name may contain, apart from the wildcards.
    /// </summary>
    internal static readonly char[] INVALID_PATTERN_CHARACTERS = Path.GetInvalidFileNameChars()
        .Where(character => character is not '*' and not '?')
        .ToArray();

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(FileNamePatterns).Namespace, nameof(FileNamePatterns));

    public static string[] Parse(string? patterns) => (patterns ?? string.Empty)
        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public static string Normalize(string? patterns) => string.Join(';', Parse(patterns));

    public static bool Matches(string filePath, IReadOnlyList<string> patterns)
    {
        var fileName = Path.GetFileName(filePath);
        return !string.IsNullOrWhiteSpace(fileName)
               && patterns.Any(pattern => MatchesPattern(filePath, fileName, pattern));
    }

    /// <summary>
    /// Whether one of the patterns asks for a hidden file by itself.
    /// </summary>
    /// <remarks>
    /// As in a shell, only a pattern which begins with a dot, such as .gitignore or .*, selects a
    /// hidden file while hidden files are left out. Neither * nor a selector does, so .env does not
    /// end up in an index by accident, cf. DirectoryFileTraversal.
    /// </remarks>
    public static bool NamesHiddenFile(string filePath, IReadOnlyList<string> patterns)
    {
        var fileName = Path.GetFileName(filePath);
        return !string.IsNullOrWhiteSpace(fileName)
               && patterns.Any(pattern => pattern.StartsWith(DirectoryFileTraversal.HIDDEN_NAME_PREFIX)
                                          && FileSystemName.MatchesSimpleExpression(pattern, fileName, ignoreCase: true));
    }

    /// <summary>
    /// Whether the patterns may select audio or video files, which a data source sends to the transcription provider.
    /// </summary>
    /// <remarks>
    /// Deliberately cautious, since it answers without looking at a single file: a pattern which
    /// may match any extension, such as * or meeting*, counts. The selectors never select media.
    /// </remarks>
    /// <param name="patterns">The parsed patterns.</param>
    /// <returns>True when audio or video files may match.</returns>
    public static bool MaySelectMedia(IReadOnlyList<string> patterns) => patterns.Any(MaySelectMedia);

    private static bool MaySelectMedia(string pattern)
    {
        if (pattern.StartsWith(SELECTOR_DELIMITER) && pattern.EndsWith(SELECTOR_DELIMITER))
            return false;

        var extensionStart = pattern.LastIndexOf('.');
        if (extensionStart < 0)
            return pattern.IndexOfAny(['*', '?']) >= 0;

        var extension = pattern[(extensionStart + 1)..];
        if (extension.IndexOfAny(['*', '?']) >= 0)
            return true;

        return FileTypes.IsAllowedPath($"media.{extension}", FileTypes.AUDIO, FileTypes.VIDEO);
    }

    public static bool AreEquivalent(IReadOnlyCollection<string>? left, IReadOnlyCollection<string>? right)
    {
        if (left is null || right is null)
            return left is null && right is null;

        return left.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(right);
    }

    public static bool IsValid(string? patterns) => GetValidationIssue(patterns) is ValidationIssue.NONE;

    /// <summary>
    /// Validates a semicolon-separated list of simple file-name patterns and semantic selectors.
    /// </summary>
    public static string? Validate(string? patterns) => GetValidationIssue(patterns) switch
    {
        ValidationIssue.NONE => null,
        ValidationIssue.MISSING => TB("Please provide at least one file pattern, e.g., :documents: or *.txt. Separate multiple patterns with a semicolon."),
        ValidationIssue.EMPTY_ENTRY => TB("Please remove empty file patterns. Separate valid patterns with a single semicolon."),
        ValidationIssue.UNKNOWN_SELECTOR => TB("One of the file patterns is an unknown selector. Please use :documents: or :text:, or a file name pattern such as *.txt."),
        ValidationIssue.RECURSIVE_WILDCARD => TB("Please use only single asterisks as wildcards, e.g., *.txt or report-*.md."),
        ValidationIssue.PATH => TB("Please use file name patterns without folder paths, e.g., *.txt or report-*.md."),
        ValidationIssue.INVALID_CHARACTER => TB("One of the file patterns contains an invalid character."),
        _ => throw new ArgumentOutOfRangeException(nameof(patterns)),
    };

    private static ValidationIssue GetValidationIssue(string? patterns)
    {
        if (string.IsNullOrWhiteSpace(patterns))
            return ValidationIssue.MISSING;

        var individualPatterns = patterns.Split(';');
        if (individualPatterns.Any(string.IsNullOrWhiteSpace))
            return ValidationIssue.EMPTY_ENTRY;

        foreach (var patternEntry in individualPatterns)
        {
            var pattern = patternEntry.Trim();
            if (IsSelector(pattern))
                continue;

            //
            // Colons are allowed in file names on macOS and Linux, so a mistyped selector such as
            // ':document:' would be a valid pattern which matches nothing. Every file would then
            // leave the index:
            //
            if (LooksLikeSelector(pattern))
                return ValidationIssue.UNKNOWN_SELECTOR;

            if (pattern.Contains("**", StringComparison.Ordinal))
                return ValidationIssue.RECURSIVE_WILDCARD;

            if (pattern is "." or ".."
                || pattern.EndsWith("..", StringComparison.Ordinal)
                || pattern.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '/', '\\']) >= 0)
                return ValidationIssue.PATH;

            if (pattern.IndexOfAny(INVALID_PATTERN_CHARACTERS) >= 0)
                return ValidationIssue.INVALID_CHARACTER;
        }

        return ValidationIssue.NONE;
    }

    private static bool MatchesPattern(string filePath, string fileName, string pattern)
    {
        //
        // Media never matches a selector: a data source cannot index it, and an SVG or a renamed
        // file would pass the check for readable text. The media check looks at the extension
        // only, so it comes first and saves reading the file:
        //
        if (IsSelector(pattern, ALL_DOCUMENTS_PATTERN))
            return !IsMediaFile(filePath) && FileTypes.IsAllowedPath(filePath, FileTypes.DOCUMENT);

        if (IsSelector(pattern, ALL_TEXT_FILES_PATTERN))
            return !IsMediaFile(filePath) && FileTypes.IsAllowedPath(filePath, FileTypes.READABLE_TEXT);

        return FileSystemName.MatchesSimpleExpression(pattern, fileName, ignoreCase: true);
    }

    internal static bool IsSelector(string pattern) => IsSelector(pattern, ALL_DOCUMENTS_PATTERN) || IsSelector(pattern, ALL_TEXT_FILES_PATTERN);

    private static bool IsSelector(string pattern, string selector) => string.Equals(pattern, selector, StringComparison.OrdinalIgnoreCase);

    internal static bool LooksLikeSelector(string pattern) => pattern.StartsWith(SELECTOR_DELIMITER) && pattern.EndsWith(SELECTOR_DELIMITER);

    internal static bool IsMediaFile(string filePath) => FileTypes.IsAllowedPath(filePath, FileTypes.MEDIA);
}
