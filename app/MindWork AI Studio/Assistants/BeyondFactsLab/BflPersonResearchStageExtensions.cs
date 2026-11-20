namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// What the BeyondFacts Lab shows and loads for a stage of the research on one person.
/// </summary>
public static class BflPersonResearchStageExtensions
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(BflPersonResearchStageExtensions).Namespace, nameof(BflPersonResearchStageExtensions));

    /// <param name="stage">The stage.</param>
    extension(BflPersonResearchStage stage)
    {
        /// <summary>
        /// Gets the title of the stage.
        /// </summary>
        public string Title => stage switch
        {
            BflPersonResearchStage.PROFILE => TB("Profile"),
            BflPersonResearchStage.POSITIONS => TB("Positions"),
            BflPersonResearchStage.APPEARANCES => TB("Public appearances"),

            _ => string.Empty,
        };

        /// <summary>
        /// Gets what the agent of the stage looks for.
        /// </summary>
        public string Description => stage switch
        {
            BflPersonResearchStage.PROFILE => TB("Who the person is: their function, professional career, and education."),
            BflPersonResearchStage.POSITIONS => TB("What the person has said in public on the topic of the event."),
            BflPersonResearchStage.APPEARANCES => TB("How the person argues in speeches, interviews, and discussions."),

            _ => string.Empty,
        };

        /// <summary>
        /// Gets the stem of the file with the task of the stage, see BflPromptLibrary.
        /// </summary>
        public string FileStem => stage switch
        {
            BflPersonResearchStage.PROFILE => "profile",
            BflPersonResearchStage.POSITIONS => "positions",
            BflPersonResearchStage.APPEARANCES => "appearances",

            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "The stage has no files."),
        };

        /// <summary>
        /// Gets the stages, in the order in which they run.
        /// </summary>
        public static IReadOnlyList<BflPersonResearchStage> All => Enum.GetValues<BflPersonResearchStage>();
    }
}