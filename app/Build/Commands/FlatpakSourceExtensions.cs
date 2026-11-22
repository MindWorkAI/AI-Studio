namespace Build.Commands;

public static class FlatpakSourceExtensions
{
    /// <param name="source">The state of AI Studio which goes into the Flatpak.</param>
    extension(FlatpakSource source)
    {
        /// <summary>
        /// The name of the source for the option --source.
        /// </summary>
        public string Name => source switch
        {
            FlatpakSource.MAIN => "main",
            FlatpakSource.PULL_REQUEST => "pr",

            _ => string.Empty,
        };

        /// <summary>
        /// What the source builds, and what for.
        /// </summary>
        public string Description => source switch
        {
            FlatpakSource.MAIN => "main of MindWorkAI/AI-Studio, to simulate a pre-release",
            FlatpakSource.PULL_REQUEST => "a pull request of MindWorkAI/AI-Studio, to test its changes early",

            _ => string.Empty,
        };

        /// <summary>
        /// All sources the command offers, in the order of its menu.
        /// </summary>
        public static IReadOnlyList<FlatpakSource> All => [FlatpakSource.MAIN, FlatpakSource.PULL_REQUEST];

        /// <summary>
        /// Finds the source for its name.
        /// </summary>
        /// <param name="name">The name of the source, as the option --source takes it.</param>
        /// <returns>The source, or NONE for an unknown name.</returns>
        public static FlatpakSource FromName(string name) => FlatpakSource.All.FirstOrDefault(candidate => string.Equals(candidate.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}