namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Decides whom of the participants the chat of the step "Test situation" of the BeyondFacts Lab may play.
/// </summary>
/// <remarks>
/// Only a person with a documented public position qualifies, so that the chat never makes up what somebody stands
/// for: the person stands on the current list of participants, and the stage "Positions" of the research on them is
/// done and holds text. For an event which is not public, no research counts, see BflEventVisibility, and nobody
/// qualifies. The prompt of the simulation still lets the chat refuse to play a person whose dossier documents no
/// position after all, since only the model reads what the text says.
/// </remarks>
public static class BflSimulationCandidates
{
    /// <summary>
    /// Gets the people the chat of the step "Test situation" may play.
    /// </summary>
    /// <param name="forms">The forms of the preparation, by their steps.</param>
    /// <param name="topics">The research on the topics, with the list of participants.</param>
    /// <param name="people">The research on every person stored for the preparation.</param>
    /// <returns>The research on each person who qualifies, in the order of the list.</returns>
    public static IReadOnlyList<BflPersonResearch> Of(IReadOnlyDictionary<BflStep, BflStepFormData> forms, IReadOnlyDictionary<BflResearchTopic, BflResearch> topics, IReadOnlyList<BflPersonResearch> people)
    {
        if (BflEventVisibility.IsNotPublic(forms))
            return [];

        return [.. BflStepResearch.Of(topics, people).People.Where(HasPositions)];
    }

    /// <summary>
    /// Tells whether the research on a person documents public positions: its stage "Positions" is done and holds text.
    /// </summary>
    /// <param name="person">The research on the person.</param>
    public static bool HasPositions(BflPersonResearch person) => person.StageOf(BflPersonResearchStage.POSITIONS) is { } positions && positions.Text.Trim().Length > 0;
}