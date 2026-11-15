namespace AIStudio.Assistants.BeyondFactsLab;

public static class BflFormSectionExtensions
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(BflFormSectionExtensions).Namespace, nameof(BflFormSectionExtensions));

    /// <param name="section">The section.</param>
    extension(BflFormSection section)
    {
        /// <summary>
        /// Gets the heading of the section, in the words of the person; empty for fields without a section.
        /// </summary>
        public string Title => section switch
        {
            BflFormSection.WHAT_MATTERS_TO_ME => TB("What matters to me"),
            BflFormSection.WHAT_TOUCHES_ME => TB("What touches me"),
            BflFormSection.WHAT_MOVES_OTHERS => TB("What moves others"),

            _ => string.Empty,
        };

        /// <summary>
        /// Gets whether the section holds the card sort, above its fields.
        /// </summary>
        public bool HasCardSort => section is BflFormSection.WHAT_MATTERS_TO_ME;
    }
}