namespace AIStudio.Tools.Glossaries;

/// <summary>
/// Why a glossary file yields no glossary.
/// </summary>
public enum GlossaryLoadIssue
{
    /// <summary>
    /// The file was read and holds at least one table.
    /// </summary>
    NONE,

    /// <summary>
    /// The file does not exist, or is not reachable, e.g. on a network share which is offline.
    /// </summary>
    FILE_NOT_FOUND,

    /// <summary>
    /// The runtime could not read the file.
    /// </summary>
    UNREADABLE,

    /// <summary>
    /// The file was read, but holds no table with a header row, two columns, and one entry.
    /// </summary>
    NO_TABLE,
}