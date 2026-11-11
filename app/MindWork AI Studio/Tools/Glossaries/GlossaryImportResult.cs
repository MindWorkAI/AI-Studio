namespace AIStudio.Tools.Glossaries;

/// <summary>
/// The entries of a glossary after an import, and what the import changed.
/// </summary>
/// <param name="Entries">All entries of the glossary after the import.</param>
/// <param name="Added">How many entries the import added.</param>
/// <param name="Replaced">How many existing entries the import replaced, because they have the same term.</param>
/// <param name="Removed">How many existing entries the import removed, which only replacing all of them does.</param>
/// <param name="SkippedRows">How many rows of the table the import left out, because their term or value is empty.</param>
public sealed record GlossaryImportResult(IReadOnlyList<GlossaryEntry> Entries, int Added, int Replaced, int Removed, int SkippedRows);