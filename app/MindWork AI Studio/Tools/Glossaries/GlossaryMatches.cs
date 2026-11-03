using AIStudio.Settings;

namespace AIStudio.Tools.Glossaries;

/// <summary>
/// The entries of one glossary whose terms occur in a text.
/// </summary>
/// <param name="Glossary">The glossary.</param>
/// <param name="Entries">The matching entries.</param>
/// <param name="LoadFailed">True when the entries of the glossary could not be read, so it has no entries here
/// although its terms might occur in the text.</param>
public sealed record GlossaryMatches(Glossary Glossary, IReadOnlyList<GlossaryEntry> Entries, bool LoadFailed = false);