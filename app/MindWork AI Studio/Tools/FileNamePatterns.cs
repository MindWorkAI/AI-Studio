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
    /// A semantic pattern for every file whose contents can be read as text, including files
    /// without a known extension. Unlike <c>*</c>, it does not select media or binary documents.
    /// </summary>
    public const string ALL_TEXT_FILES_PATTERN = ":text:";

    private enum ValidationIssue
    {
        NONE,
        MISSING,
        EMPTY_ENTRY,
        RECURSIVE_WILDCARD,
        PATH,
        INVALID_CHARACTER,
    }

    private static readonly char[] INVALID_PATTERN_CHARACTERS = Path.GetInvalidFileNameChars()
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
               && patterns.Any(pattern => IsAllTextFilesPattern(pattern)
                   ? IsReadableNonMediaFile(filePath)
                   : FileSystemName.MatchesSimpleExpression(pattern, fileName, ignoreCase: true));
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
        ValidationIssue.MISSING => TB("Please provide at least one file pattern, e.g., *.txt or :text:. Separate multiple patterns with a semicolon."),
        ValidationIssue.EMPTY_ENTRY => TB("Please remove empty file patterns. Separate valid patterns with a single semicolon."),
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
            if (IsAllTextFilesPattern(pattern))
                continue;

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

    private static bool IsAllTextFilesPattern(string pattern) => string.Equals(pattern, ALL_TEXT_FILES_PATTERN, StringComparison.OrdinalIgnoreCase);

    private static bool IsReadableNonMediaFile(string filePath)
    {
        return !FileTypes.IsAllowedPath(filePath, FileTypes.AUDIO, FileTypes.VIDEO)
               && FileTypes.IsAllowedPath(filePath, FileTypes.READABLE_TEXT);
    }
}
