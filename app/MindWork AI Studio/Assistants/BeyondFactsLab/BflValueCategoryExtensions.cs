namespace AIStudio.Assistants.BeyondFactsLab;

public static class BflValueCategoryExtensions
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(BflValueCategoryExtensions).Namespace, nameof(BflValueCategoryExtensions));

    /// <param name="category">The category.</param>
    extension(BflValueCategory category)
    {
        /// <summary>
        /// Gets the name of the category, as the buttons of the card sort show it.
        /// </summary>
        public string Title => category switch
        {
            BflValueCategory.ESPECIALLY_IMPORTANT => TB("Especially important"),
            BflValueCategory.ALSO_IMPORTANT => TB("Also important"),
            BflValueCategory.LESS_IMPORTANT => TB("Less important for this occasion"),
            BflValueCategory.UNCLEAR => TB("Unclear"),

            _ => TB("Not sorted yet"),
        };

        /// <summary>
        /// Gets every category a person can choose, in the order the buttons show them.
        /// </summary>
        public static IReadOnlyList<BflValueCategory> All => [BflValueCategory.ESPECIALLY_IMPORTANT, BflValueCategory.ALSO_IMPORTANT, BflValueCategory.LESS_IMPORTANT, BflValueCategory.UNCLEAR];
    }
}