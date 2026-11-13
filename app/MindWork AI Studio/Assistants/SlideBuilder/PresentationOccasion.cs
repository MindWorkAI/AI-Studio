namespace AIStudio.Assistants.SlideBuilder;

/// <summary>
/// The occasion of a presentation, which shapes how the LLM structures it.
/// </summary>
/// <remarks>
/// The list covers the common occasions only; OTHER takes any other one as text.
/// </remarks>
public enum PresentationOccasion
{
    UNSPECIFIED = 0,

    TALK,
    KEYNOTE,
    CONFERENCE_TALK,
    LECTURE,
    TRAINING,
    WORKSHOP,
    MULTI_DAY_WORKSHOP,
    THESIS_DEFENSE,
    RESEARCH_RESULTS,
    PROJECT_REPORT,
    MANAGEMENT_REPORT,
    PITCH,
    PRODUCT_PRESENTATION,
    KICK_OFF,
    TEAM_MEETING,

    OTHER,
}