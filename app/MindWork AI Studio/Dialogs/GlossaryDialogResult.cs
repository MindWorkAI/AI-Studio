using AIStudio.Settings;
using AIStudio.Tools.Glossaries;

namespace AIStudio.Dialogs;

public sealed record GlossaryDialogResult(Glossary Glossary, IReadOnlyList<GlossaryEntry> Entries);