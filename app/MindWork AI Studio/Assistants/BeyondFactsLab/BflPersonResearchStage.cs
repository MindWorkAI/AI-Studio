namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// A stage of the research on one person of the BeyondFacts Lab. Each stage is an agent of its own, with a budget of
/// its own, and the stages run in the order of this enumeration: a later stage builds on what the earlier ones found.
/// </summary>
public enum BflPersonResearchStage
{
    /// <summary>
    /// Finds the right person and describes their function, professional career, and education.
    /// </summary>
    PROFILE,

    /// <summary>
    /// Describes what the person said in public on the topic of the event.
    /// </summary>
    POSITIONS,

    /// <summary>
    /// Describes how the person argues in public, in speeches, interviews, and discussions.
    /// </summary>
    APPEARANCES,
}