using AIStudio.Tools.Glossaries;

namespace AIStudio.Dialogs;

public sealed record GlossaryImportDialogResult(IReadOnlyList<GlossaryEntry> Entries, string SuggestedName, CommonLanguages? SuggestedSourceLanguage, CommonLanguages? SuggestedTargetLanguage);