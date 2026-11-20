namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// What the agent of one stage found about a person, as a part of BflPersonResearch.
/// </summary>
public sealed class BflPersonStageResearch
{
    /// <summary>
    /// Gets or sets the stage.
    /// </summary>
    public BflPersonResearchStage Stage { get; set; }

    /// <summary>
    /// Gets or sets what the agent found and what left AI Studio for it, like the research on a topic.
    /// </summary>
    public BflResearch Research { get; set; } = new();
}