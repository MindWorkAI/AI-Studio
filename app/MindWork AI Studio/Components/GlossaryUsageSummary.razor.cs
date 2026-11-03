using AIStudio.Settings;
using AIStudio.Tools.Glossaries;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// Shows which glossary entries a request sent to the AI, and which chosen glossaries could not be loaded.
/// </summary>
/// <remarks>
/// When a term is translated unexpectedly, the user checks here whether its entry reached the AI at all.
/// A glossary which could not be loaded is shown apart from one without matches: the request went out
/// without it, and the user must not take that for a text which none of its terms occur in.
/// </remarks>
public partial class GlossaryUsageSummary : MSGComponentBase
{
    /// <summary>
    /// The matches of the request, one per chosen glossary.
    /// </summary>
    [Parameter]
    public IReadOnlyList<GlossaryMatches> Matches { get; set; } = [];

    private string GetHeaderText(IReadOnlyList<GlossaryMatches> sections)
    {
        var entryCount = sections.Sum(match => match.Entries.Count).CompactCount();
        return sections.Count is 1
            ? string.Format(T("Glossary entries for this translation: {0} from 1 glossary"), entryCount)
            : string.Format(T("Glossary entries for this translation: {0} from {1} glossaries"), entryCount, sections.Count.CompactCount());
    }

    private string GetGlossaryName(Glossary glossary) => glossary.Type is GlossaryType.TRANSLATION
        ? glossary.Name
        : string.Format(T("{0} (reference)"), glossary.Name);
}