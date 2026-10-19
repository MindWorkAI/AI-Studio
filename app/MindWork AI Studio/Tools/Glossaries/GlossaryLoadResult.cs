namespace AIStudio.Tools.Glossaries;

/// <summary>
/// The tables of a glossary file, or why there are none.
/// </summary>
/// <param name="Issue">Why the file yields no glossary; <see cref="GlossaryLoadIssue.NONE"/> when it does.</param>
/// <param name="Tables">The tables of the file; empty unless the file was read.</param>
public sealed record GlossaryLoadResult(GlossaryLoadIssue Issue, IReadOnlyList<GlossaryTable> Tables)
{
    public bool Success => this.Issue is GlossaryLoadIssue.NONE;

    public static GlossaryLoadResult Failed(GlossaryLoadIssue issue) => new(issue, []);
}