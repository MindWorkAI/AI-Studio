using AIStudio.Models;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Estimates whether the context window of a model holds the simulation the person chose in the step "Test situation"
/// of the BeyondFacts Lab.
/// </summary>
/// <remarks>
/// The prompt of the step carries the dossier of every person the chat plays in full, with their entry in the list:
/// unlike the material, a dossier is never shortened, since the chat must not play a person after half of what they
/// said. The estimate adds the
/// dossiers to the smallest window the lab accepts at all, which leaves room for the fixed part of the prompt, the
/// details, and the conversation, and counts the characters of a dossier the way the budget of the material does. The
/// material then gets what is left, see BflMaterialBudget.
/// </remarks>
public static class BflSimulationBudget
{
    /// <summary>
    /// Estimates how many tokens the dossiers of the people the chat plays take in the prompt.
    /// </summary>
    /// <param name="people">The people the chat plays.</param>
    /// <returns>The estimated tokens.</returns>
    public static int DossierTokens(IReadOnlyList<BflSimulatedPerson> people)
    {
        var characters = people.Sum(person => person.Participant.Entry.Trim().Length + person.Research.Stages.Sum(stage => stage.Research.Text.Trim().Length));
        return (characters + BflMaterialBudget.CHARACTERS_PER_TOKEN - 1) / BflMaterialBudget.CHARACTERS_PER_TOKEN;
    }

    /// <summary>
    /// Estimates how many tokens a model needs to read at once for a simulation.
    /// </summary>
    /// <param name="people">The people the chat plays.</param>
    /// <returns>The estimated tokens.</returns>
    public static int RequiredTokens(IReadOnlyList<BflSimulatedPerson> people) => BflModelRequirements.MINIMUM_CONTEXT_TOKENS + DossierTokens(people);

    /// <summary>
    /// Checks whether the context window of a model is likely to hold a simulation.
    /// </summary>
    /// <param name="simulation">The simulation in effect.</param>
    /// <param name="contextWindow">The context window of the model, as it ships.</param>
    /// <returns>Whether the window holds the simulation, nobody knows, or it is likely too small.</returns>
    public static BflSimulationFit Check(BflEffectiveSimulation simulation, ContextWindow contextWindow)
    {
        if (simulation.People.Count is 0)
            return BflSimulationFit.FITS;

        if (!contextWindow.IsKnown)
            return BflSimulationFit.UNKNOWN_CONTEXT;

        return contextWindow.DefaultTokens < RequiredTokens(simulation.People) ? BflSimulationFit.TOO_SMALL : BflSimulationFit.FITS;
    }
}