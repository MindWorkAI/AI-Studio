namespace AIStudio.Tools.Glossaries;

/// <summary>
/// A spreadsheet or CSV file within a configuration plugin which holds the entries of a glossary.
/// </summary>
/// <remarks>
/// The columns are named by their header, not by their position, so a column the organization adds
/// to its spreadsheet later does not shift the others.
/// </remarks>
/// <param name="FullPath">The full path of the file, within the plugin folder.</param>
/// <param name="TermColumn">The header of the column with the terms.</param>
/// <param name="ValueColumn">The header of the column with the translations or explanations.</param>
/// <param name="OtherFormsColumn">The header of the column with the other forms, separated by semicolons; null when the file has none.</param>
/// <param name="NoteColumn">The header of the column with the notes; null when the file has none.</param>
public sealed record GlossaryPluginFile(string FullPath, string TermColumn, string ValueColumn, string? OtherFormsColumn, string? NoteColumn);