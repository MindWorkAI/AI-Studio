namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Whether the context window of a model is likely to hold the simulation the person chose in the step "Test
/// situation" of the BeyondFacts Lab, see BflSimulationBudget.
/// </summary>
public enum BflSimulationFit
{
    /// <summary>
    /// The window is known and holds the simulation, or the simulation plays nobody from the research.
    /// </summary>
    FITS,

    /// <summary>
    /// Nobody knows the window of the model, so the lab cannot tell; it warns the person and lets the chat run.
    /// </summary>
    UNKNOWN_CONTEXT,

    /// <summary>
    /// The window is known and likely too small; the chat does not send.
    /// </summary>
    TOO_SMALL,
}