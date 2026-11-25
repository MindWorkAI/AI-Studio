namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// The research which the prompt of a step of the BeyondFacts Lab carries: in the step "Strategy" all of it, in every
/// other step none.
/// </summary>
/// <param name="Topics">The research on the topics, by the topics.</param>
/// <param name="People">The research on single people of the current list of participants, in the order of the list.</param>
public sealed record BflStepResearch(IReadOnlyDictionary<BflResearchTopic, BflResearch> Topics, IReadOnlyList<BflPersonResearch> People)
{
    /// <summary>
    /// The research of a step which carries none.
    /// </summary>
    public static readonly BflStepResearch NONE = new(new Dictionary<BflResearchTopic, BflResearch>(), []);

    /// <summary>
    /// Gets the research on those people who stand on the current list of participants, in the order of the list.
    /// </summary>
    /// <remarks>
    /// The research on a person who is no longer on the list stays stored, but no longer counts.
    /// </remarks>
    /// <param name="topics">The research on the topics, with the list of participants.</param>
    /// <param name="people">The research on every person stored for the preparation.</param>
    public static BflStepResearch Of(IReadOnlyDictionary<BflResearchTopic, BflResearch> topics, IReadOnlyList<BflPersonResearch> people)
    {
        var participants = topics.GetValueOrDefault(BflResearchTopic.PARTICIPANTS)?.Participants ?? [];
        var listed = participants
            .Select(participant => people.FirstOrDefault(person => participant.HasName(person.Name)))
            .OfType<BflPersonResearch>()
            .ToList();

        return new(topics, listed);
    }
}