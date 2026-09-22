namespace AIStudio.Tools;

/// <summary>
/// A table found in a message, ready to be written to a file.
/// </summary>
/// <param name="Ordinal">Which table of the message this is, counting from one. The same table
/// appears once per format we offer for it, so this is what tells two tables apart even when they
/// carry the same heading.</param>
/// <param name="Caption">What the table is about, taken from its first column heading.</param>
/// <param name="Format">The format this content is written as.</param>
/// <param name="Content">The finished file content.</param>
public sealed record MessageTable(int Ordinal, string Caption, FileExportFormat Format, string Content);