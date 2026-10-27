using AIStudio.Settings;

namespace AIStudio.Tools.Glossaries;

/// <summary>
/// The entries of one glossary whose terms occur in a text.
/// </summary>
/// <param name="Glossary">The glossary.</param>
/// <param name="Entries">The matching entries.</param>
public sealed record GlossaryMatches(Glossary Glossary, IReadOnlyList<GlossaryEntry> Entries);