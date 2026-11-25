namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Whom the chat of the step "Test situation" of the BeyondFacts Lab plays: generic counterparts, or people from the
/// research on single participants in a format of the event.
/// </summary>
/// <remarks>
/// The names are stored, so they never change. A new format joins as a new member, see BflSimulationModeExtensions.
/// </remarks>
public enum BflSimulationMode
{
    /// <summary>
    /// Generic counterparts, no specific person: the step as it was before the lab simulated people.
    /// </summary>
    GENERIC,

    /// <summary>
    /// One person from the research, after their dossier.
    /// </summary>
    SINGLE_PERSON,

    /// <summary>
    /// A panel discussion with up to three people from the research.
    /// </summary>
    PANEL_DISCUSSION,

    /// <summary>
    /// A press conference with up to five people from the research.
    /// </summary>
    PRESS_CONFERENCE,
}