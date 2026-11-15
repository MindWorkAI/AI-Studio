namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// What the BeyondFacts Lab shows and allows for a mode of the simulation in the step "Test situation".
/// </summary>
public static class BflSimulationModeExtensions
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(BflSimulationModeExtensions).Namespace, nameof(BflSimulationModeExtensions));

    /// <param name="mode">The mode.</param>
    extension(BflSimulationMode mode)
    {
        /// <summary>
        /// Gets the name of the mode, as the selection shows it.
        /// </summary>
        public string Title => mode switch
        {
            BflSimulationMode.SINGLE_PERSON => TB("One person from the research"),
            BflSimulationMode.PANEL_DISCUSSION => TB("Panel discussion with up to three people"),
            BflSimulationMode.PRESS_CONFERENCE => TB("Press conference with up to five people"),

            _ => TB("Generic counterparts, no specific person"),
        };

        /// <summary>
        /// Gets how many people from the research the chat plays at most in this mode; none for generic counterparts.
        /// </summary>
        public int MaxPeople => mode switch
        {
            BflSimulationMode.SINGLE_PERSON => 1,
            BflSimulationMode.PANEL_DISCUSSION => 3,
            BflSimulationMode.PRESS_CONFERENCE => 5,

            _ => 0,
        };

        /// <summary>
        /// Gets whether the chat plays people from the research in this mode.
        /// </summary>
        public bool HasPeople => mode.MaxPeople > 0;

        /// <summary>
        /// Gets every mode, in the order the selection shows them.
        /// </summary>
        public static IReadOnlyList<BflSimulationMode> All => Enum.GetValues<BflSimulationMode>();
    }
}