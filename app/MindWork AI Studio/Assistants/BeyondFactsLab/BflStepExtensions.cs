namespace AIStudio.Assistants.BeyondFactsLab;

public static class BflStepExtensions
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(BflStepExtensions).Namespace, nameof(BflStepExtensions));

    private static string TA(string fallbackEN) => Tools.PluginSystem.I18N.I.TA(fallbackEN, typeof(BflStepExtensions).Namespace, nameof(BflStepExtensions));

    /// <param name="step">The step.</param>
    extension(BflStep step)
    {
        /// <summary>
        /// Gets the title of the step. It names the topic, never a number.
        /// </summary>
        public string Title => step switch
        {
            BflStep.FRAMEWORK => TB("Event details"),
            BflStep.KNOWLEDGE => TB("Knowledge and uncertainty"),
            BflStep.GOALS_AND_ROLE => TB("Goals and role"),
            BflStep.VALUES_AND_PERSPECTIVES => TB("Values and perspectives"),
            BflStep.STRATEGY => TB("Strategy"),
            BflStep.TEST_SITUATION => TB("Test situation"),
            BflStep.RESULTS => TB("Results and materials"),

            _ => string.Empty,
        };

        /// <summary>
        /// Gets one or two sentences about what the step is for.
        /// </summary>
        public string Description => step switch
        {
            BflStep.FRAMEWORK => TA("What the event is about, for whom, and what you were asked to contribute."),
            BflStep.KNOWLEDGE => TA("What you know for sure, what is uncertain, and where your expertise ends."),
            BflStep.GOALS_AND_ROLE => TA("What you want to achieve, and in which role you speak."),
            BflStep.VALUES_AND_PERSPECTIVES => TA("What matters to you, what touches you, and what moves others."),
            BflStep.STRATEGY => TA("How you convey evidence, trade-offs between values, and open questions. For a public event, the lab researches the event, its participants, and the public debate on the web for this step."),
            BflStep.TEST_SITUATION => TA("Play through critical questions and opposing positions, with generic counterparts or with participants whom the lab researched in depth."),
            BflStep.RESULTS => TA("What you take away from your preparation."),

            _ => string.Empty,
        };

        /// <summary>
        /// Gets a hint for the form of the step, or an empty string when the step needs none.
        /// </summary>
        public string FormHint => step switch
        {
            BflStep.VALUES_AND_PERSPECTIVES => TA("You can fill in single fields or start right away in the chat."),

            _ => string.Empty,
        };

        /// <summary>
        /// Gets the name of the folder in which a preparation keeps the files of the step. The prompt of the step
        /// and the ID of its chat follow from this name as well.
        /// </summary>
        /// <remarks>
        /// The names are stored on disk, so they never change. They carry no number: the order of the steps comes
        /// from BflStep alone, so that a step can move or a new one can join without touching any stored file.
        /// </remarks>
        public string FolderName => step switch
        {
            BflStep.FRAMEWORK => "framework",
            BflStep.KNOWLEDGE => "knowledge",
            BflStep.GOALS_AND_ROLE => "goals",
            BflStep.VALUES_AND_PERSPECTIVES => "values",
            BflStep.STRATEGY => "strategy",
            BflStep.TEST_SITUATION => "test",
            BflStep.RESULTS => "results",

            _ => throw new ArgumentOutOfRangeException(nameof(step), step, "The step has no folder."),
        };

        /// <summary>
        /// Gets every step, in the order the lab suggests.
        /// </summary>
        public static IReadOnlyList<BflStep> All => Enum.GetValues<BflStep>();
    }
}