namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Whom the person chose for the chat of the step "Test situation" of the BeyondFacts Lab to play: the mode, and the
/// people from the research on single participants.
/// </summary>
/// <remarks>
/// The choice belongs to the details of its step and is stored with them, see BflStepFormData.Simulation. It is the
/// opt-in of the person: the chat plays a real person only while the person chose them here. What counts is the
/// effective simulation, see BflEffectiveSimulation: a name which is no longer a candidate stays stored, but counts no
/// longer, and so do the names beyond what the mode allows.
/// </remarks>
public sealed class BflSimulation
{
    /// <summary>
    /// Gets or sets the mode.
    /// </summary>
    public BflSimulationMode Mode { get; set; } = BflSimulationMode.GENERIC;

    /// <summary>
    /// Gets or sets the names of the people the person chose, as the list of participants gives them, in no particular
    /// order.
    /// </summary>
    public List<string> People { get; set; } = [];

    /// <summary>
    /// Tells whether the person chose a person, regardless of case and of the spaces between the words.
    /// </summary>
    /// <param name="name">The name of the person.</param>
    public bool HasChosen(string name) => this.People.Any(chosen => string.Equals(BflParticipant.NameKey(chosen), BflParticipant.NameKey(name), StringComparison.Ordinal));
}