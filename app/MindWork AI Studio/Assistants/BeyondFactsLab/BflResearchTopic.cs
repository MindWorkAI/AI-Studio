namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// What a research agent of the BeyondFacts Lab looks into on the web, one agent for each topic.
/// </summary>
/// <remarks>
/// A single answer of a model may call tools only so often, see ToolSelectionRules.MAX_TOOL_CALLS. Each topic gets an
/// agent of its own, with a budget of its own, so that the participants, for example, get the searches they need for
/// simulating them later. Each topic is stored in a file of its own, see BflResearchTopicExtensions.FileName.
/// </remarks>
public enum BflResearchTopic
{
    /// <summary>
    /// The event itself: its organizer, format and schedule, moderation, and earlier editions.
    /// </summary>
    EVENT,

    /// <summary>
    /// The other participants: their roles and their public positions on the topic of the event.
    /// </summary>
    PARTICIPANTS,

    /// <summary>
    /// The public debate about the topic of the event: positions, points of dispute, and the framing of others.
    /// </summary>
    DEBATE,
}