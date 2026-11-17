namespace AIStudio.Assistants.BeyondFactsLab;

public static class BflResearchTopicExtensions
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(BflResearchTopicExtensions).Namespace, nameof(BflResearchTopicExtensions));

    /// <param name="topic">The topic of the research.</param>
    extension(BflResearchTopic topic)
    {
        /// <summary>
        /// Gets the name of the topic.
        /// </summary>
        public string Title => topic switch
        {
            BflResearchTopic.EVENT => TB("The event"),
            BflResearchTopic.PARTICIPANTS => TB("Participants"),
            BflResearchTopic.DEBATE => TB("Debate and framing"),

            _ => string.Empty,
        };

        /// <summary>
        /// Gets what the agent of the topic looks into.
        /// </summary>
        public string Description => topic switch
        {
            BflResearchTopic.EVENT => TB("Organizer, format and schedule, moderation, and earlier editions of the event."),
            BflResearchTopic.PARTICIPANTS => TB("Who else speaks or moderates, in which role, and which public positions they have taken on the topic."),
            BflResearchTopic.DEBATE => TB("How the topic is discussed in public: positions, points of dispute, and the framing of others."),

            _ => string.Empty,
        };

        /// <summary>
        /// Gets the name of the topic in the names of its files: its prompt and its result.
        /// </summary>
        /// <remarks>
        /// The results are stored on disk, so these names never change.
        /// </remarks>
        public string FileStem => topic switch
        {
            BflResearchTopic.EVENT => "event",
            BflResearchTopic.PARTICIPANTS => "participants",
            BflResearchTopic.DEBATE => "debate",

            _ => throw new ArgumentOutOfRangeException(nameof(topic), topic, "The topic has no files."),
        };

        /// <summary>
        /// Gets the name of the file in which a preparation keeps the result of the topic.
        /// </summary>
        public string FileName => $"{topic.FileStem}.json";

        /// <summary>
        /// Gets every topic, in the order the step "Strategy" shows them.
        /// </summary>
        public static IReadOnlyList<BflResearchTopic> All => Enum.GetValues<BflResearchTopic>();
    }
}