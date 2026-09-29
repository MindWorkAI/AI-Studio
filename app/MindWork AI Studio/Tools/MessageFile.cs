namespace AIStudio.Tools;

/// <summary>
/// A file found in a message, ready to be written: a table the model wrote, or a code block the
/// model marked as a format we write.
/// </summary>
/// <param name="Ordinal">Which table or which code block of the message this is, counting from one
/// within its kind; the format tells the two kinds apart, see FileExportFormatExtensions.IsTabular.
/// This is what tells two files of one kind apart even when they carry the same heading.</param>
/// <param name="Caption">What the file is about: the heading above it, or else the first column
/// heading of a table. Empty for a code block without a heading above it.</param>
/// <param name="Format">The format this content is written as.</param>
/// <param name="Content">The finished file content.</param>
public sealed record MessageFile(int Ordinal, string Caption, FileExportFormat Format, string Content);