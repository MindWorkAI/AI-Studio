namespace AIStudio.Assistants.ModelComparison;

public static class ModelComparisonModeExtensions
{
    /// <param name="mode">The mode of the Model Comparison assistant.</param>
    extension(ModelComparisonMode mode)
    {
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