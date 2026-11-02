namespace AIStudio.Tools.Glossaries;

/// <summary>
/// One entry of a glossary.
/// </summary>
/// <param name="Term">The term to look for in a text.</param>
/// <param name="Value">The translation or the explanation of the term, depending on the type of the glossary.</param>
/// <param name="Aliases">Further spellings or forms of the term, e.g. a plural, which count as the term.</param>
/// <param name="Note">A remark of the user about the entry, e.g. when it applies.</param>
public sealed record GlossaryEntry(string Term, string Value, IReadOnlyList<string>? Aliases = null, string Note = "");