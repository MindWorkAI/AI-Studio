namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Whom the chat of the step "Test situation" of the BeyondFacts Lab actually plays: the mode and the people which
/// count of what the person chose.
/// </summary>
/// <remarks>
/// A chosen person counts while they are a candidate, see BflSimulationCandidates, and only as many as the mode allows,
/// in the order of the list of participants. When nobody of the chosen people counts, the chat plays generic
/// counterparts, as it did before the person chose a mode.
/// </remarks>
/// <param name="Mode">The mode in effect.</param>
/// <param name="People">The research on the people the chat plays, in the order of the list; empty for generic counterparts.</param>
public sealed record BflEffectiveSimulation(BflSimulationMode Mode, IReadOnlyList<BflPersonResearch> People)
{
    /// <summary>
    /// The simulation with generic counterparts, no specific person.
    /// </summary>
    public static readonly BflEffectiveSimulation GENERIC = new(BflSimulationMode.GENERIC, []);

    /// <summary>
    /// Gets whom the chat plays of what the person chose.
    /// </summary>
    /// <param name="simulation">What the person chose; null when they chose nothing.</param>
    /// <param name="candidates">The people the chat may play, in the order of the list, see BflSimulationCandidates.</param>
    /// <returns>The simulation in effect.</returns>
    public static BflEffectiveSimulation Of(BflSimulation? simulation, IReadOnlyList<BflPersonResearch> candidates)
    {
        if (simulation is null || !simulation.Mode.HasPeople)
            return GENERIC;

        var people = candidates.Where(candidate => simulation.HasChosen(candidate.Name)).Take(simulation.Mode.MaxPeople).ToList();
        return people.Count is 0 ? GENERIC : new(simulation.Mode, people);
    }
}