namespace AIStudio.Tools.Glossaries;

/// <summary>
/// The tables of a file to import into a glossary, or why there are none.
/// </summary>
/// <param name="Issue">Why the file yields nothing to import; <see cref="GlossaryImportIssue.NONE"/> when it does.</param>
/// <param name="Tables">The tables of the file; empty unless the file was read.</param>
public sealed record GlossaryImportFile(GlossaryImportIssue Issue, IReadOnlyList<GlossaryImportTable> Tables)
{
    public bool Success => this.Issue is GlossaryImportIssue.NONE;

    public static GlossaryImportFile Failed(GlossaryImportIssue issue) => new(issue, []);
}