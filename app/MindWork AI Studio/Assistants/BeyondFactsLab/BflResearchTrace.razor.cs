using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Shows what left AI Studio for a research of the BeyondFacts Lab: the queries sent to the web search and the pages
/// opened, so that the person can check that nothing else of their preparation went out.
/// </summary>
public partial class BflResearchTrace : MSGComponentBase
{
    /// <summary>
    /// Gets or sets the research, on a topic or a stage of the research on a person.
    /// </summary>
    [Parameter, EditorRequired]
    public BflResearch Research { get; set; } = new();
}