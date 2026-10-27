namespace AIStudio.Tools.Glossaries;

/// <summary>
/// One table of a file to import into a glossary, e.g. one sheet of a spreadsheet.
/// </summary>
/// <param name="Columns">The names of the columns, as the header row of the file states them.</param>
/// <param name="Rows">The rows below the header; every row has exactly one cell per column, empty where the file has none.</param>
public sealed record GlossaryImportTable(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Rows);