namespace AIStudio.Tools.Glossaries;

/// <summary>
/// Which columns of a table to import fill which parts of the glossary entries.
/// </summary>
/// <param name="TermColumn">The index of the column with the terms.</param>
/// <param name="ValueColumn">The index of the column with the translations or explanations.</param>
/// <param name="AliasesColumn">The index of the column with further spellings of the terms, separated by semicolons; null when the table has none.</param>
/// <param name="NoteColumn">The index of the column with remarks about the entries; null when the table has none.</param>
public sealed record GlossaryColumnMapping(int TermColumn, int ValueColumn, int? AliasesColumn = null, int? NoteColumn = null);