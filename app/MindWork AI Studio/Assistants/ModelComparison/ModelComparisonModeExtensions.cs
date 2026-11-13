namespace AIStudio.Assistants.ModelComparison;

public static class ModelComparisonModeExtensions
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(ModelComparisonModeExtensions).Namespace, nameof(ModelComparisonModeExtensions));

    /// <param name="mode">The mode of the Model Comparison assistant.</param>
    extension(ModelComparisonMode mode)
    {
        /// <summary>
        /// The short name of the mode, which tells the user in which of the two modes they are.
        /// </summary>
        public string Name => mode switch
        {
            ModelComparisonMode.TASKS => TB("Field Test"),

            _ => TB("Answer Test"),
        };

        /// <summary>
        /// The title of the page of the mode, which names the assistant together with the mode.
        /// </summary>
        public string PageTitle => string.Format(TB("Model Comparison: {0}"), mode.Name);

        /// <summary>
        /// The page of the mode.
        /// </summary>
        public string Route => mode switch
        {
            ModelComparisonMode.TASKS => Routes.ASSISTANT_MODEL_COMPARISON_TASKS,

            _ => Routes.ASSISTANT_MODEL_COMPARISON_STABILITY,
        };

        /// <summary>
        /// The icon of the mode.
        /// </summary>
        public string Icon => mode switch
        {
            ModelComparisonMode.TASKS => AppIcons.COMPARISON_TASKS,

            _ => AppIcons.COMPARISON_STABILITY,
        };
    }
}