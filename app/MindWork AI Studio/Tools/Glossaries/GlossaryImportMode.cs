namespace AIStudio.Tools.Glossaries;

/// <summary>
/// What an import does with the entries a glossary holds already.
/// </summary>
public enum GlossaryImportMode
{
    /// <summary>
    /// The imported entries are added; an entry with the same term is replaced by the imported one.
    /// </summary>
    MERGE,

    /// <summary>
    /// The imported entries replace all entries of the glossary.
    /// </summary>
    REPLACE,
}