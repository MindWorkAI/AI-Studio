namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// A person whom the chat of the step "Test situation" of the BeyondFacts Lab may play: their entry in the list of
/// participants, with their role at the event, and the research on them.
/// </summary>
/// <param name="Participant">The person, as the list of participants gives them.</param>
/// <param name="Research">What the agents found on the web about the person.</param>
public sealed record BflSimulatedPerson(BflParticipant Participant, BflPersonResearch Research)
{
    /// <summary>
    /// Gets the name of the person, as the list of participants gives it.
    /// </summary>
    public string Name => this.Participant.Name;
}