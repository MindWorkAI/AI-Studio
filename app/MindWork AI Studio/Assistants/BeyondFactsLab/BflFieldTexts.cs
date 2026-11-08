namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// The texts of the fields and options of the BeyondFacts Lab, in the language of the app.
/// </summary>
public static class BflFieldTexts
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(BflFieldTexts).Namespace, nameof(BflFieldTexts));

    /// <param name="definition">The field.</param>
    extension(BflFieldDefinition definition)
    {
        /// <summary>
        /// Gets the label of the field.
        /// </summary>
        public string Label => definition.Id switch
        {
            BflFieldCatalog.FORMAT => TB("Format of the event"),
            BflFieldCatalog.EVENT_TOPIC => TB("Topic of the event"),
            BflFieldCatalog.AUDIENCE => TB("Audience"),
            BflFieldCatalog.CONTRIBUTION_TOPIC => TB("Topic of your contribution or your specific assignment"),
            BflFieldCatalog.EVENT_DATE => TB("Date and time"),
            BflFieldCatalog.EVENT_LOCATION => TB("Place"),
            BflFieldCatalog.EVENT_DURATION => TB("Total duration of the event"),
            BflFieldCatalog.CONTRIBUTION_DURATION => TB("Duration of your contribution"),
            BflFieldCatalog.OTHER_PARTICIPANTS => TB("Other participants"),
            BflFieldCatalog.EVENT_LINK => TB("Link to the event"),

            BflFieldCatalog.CONTRIBUTION_SUMMARY => TB("Short description of your contribution"),
            BflFieldCatalog.CORE_THESES => TB("Core theses and arguments (if any)"),
            BflFieldCatalog.UNCERTAINTIES => TB("Uncertainties and controversies in content and science (if known)"),
            BflFieldCatalog.MATERIAL_NOTES => TB("Notes on how to use your materials"),

            BflFieldCatalog.GOALS => TB("What do you want to achieve with your contribution?"),
            BflFieldCatalog.GOAL_DIRECTIONS => TB("Suggestions"),

            BflFieldCatalog.VALUES_AND_CONCERNS => TB("Values and concerns: What matters most to you about this topic?"),
            BflFieldCatalog.PERSONAL_CONCERN => TB("Personal involvement: Where does the topic touch you personally? Which question or criticism could trigger a strong reaction?"),
            BflFieldCatalog.OTHER_PERSPECTIVES => TB("Other perspectives: Whose point of view would you like to understand better, or have you hardly considered so far?"),

            BflFieldCatalog.COMMUNICATION_WORRIES => TB("Uncertainties and worries about communicating"),

            BflFieldCatalog.QUESTION_TYPE => TB("Type of questions"),
            BflFieldCatalog.DIFFICULTY => TB("Difficulty at the start"),
            BflFieldCatalog.TEST_NOTES => TB("Special notes for the test"),

            BflFieldCatalog.POSITION_CHANGES => TB("What have you changed, kept, or left open compared to your initial position, and why?"),

            _ => string.Empty,
        };

        /// <summary>
        /// Gets the help text below the field, or an empty string when it needs none.
        /// </summary>
        public string Hint => definition.Id switch
        {
            BflFieldCatalog.FORMAT => TB("E.g., panel, keynote, or interview; online, on site, or hybrid"),
            BflFieldCatalog.EVENT_TOPIC => TB("The central question or the title"),
            BflFieldCatalog.AUDIENCE => TB("E.g., students, experts, or the general public"),
            BflFieldCatalog.CONTRIBUTION_TOPIC => TB("Stays in your preparation and never goes into the research on the web"),
            BflFieldCatalog.EVENT_DATE => TB("E.g., December 10, 2 pm"),
            BflFieldCatalog.EVENT_LOCATION => TB("E.g., the venue and the city, or online"),
            BflFieldCatalog.EVENT_DURATION => TB("E.g., 90 minutes"),
            BflFieldCatalog.CONTRIBUTION_DURATION => TB("E.g., 8 minutes"),
            BflFieldCatalog.OTHER_PARTICIPANTS => TB("Names and roles, as far as you know them"),
            BflFieldCatalog.EVENT_LINK => TB("The address of the event page, beginning with https://"),

            BflFieldCatalog.CONTRIBUTION_SUMMARY => TB("A few sentences are enough"),
            BflFieldCatalog.MATERIAL_NOTES => TB("E.g., use only paper X as the basis for the content"),

            BflFieldCatalog.GOALS => TB("In your own words, with the suggestions below, or still open: the chat sharpens your goal and role with you."),

            BflFieldCatalog.PERSONAL_CONCERN => TB("Personal detail: it goes into exports and shared sessions only when you explicitly choose so."),

            BflFieldCatalog.COMMUNICATION_WORRIES => TB("E.g., aggressive moderation, time pressure, or a particular counterpart"),

            BflFieldCatalog.DIFFICULTY => TB("You can raise the difficulty after each question."),
            BflFieldCatalog.TEST_NOTES => TB("E.g., take up a confirmed conflict of values or a perspective from the step about values and perspectives"),

            _ => string.Empty,
        };
    }

    /// <param name="option">The option.</param>
    extension(BflFieldOption option)
    {
        /// <summary>
        /// Gets the label of the option.
        /// </summary>
        public string Label => option.Id switch
        {
            BflFieldCatalog.GOAL_INFORM => TB("Inform"),
            BflFieldCatalog.GOAL_MODERATE => TB("Moderate"),
            BflFieldCatalog.GOAL_TAKE_POSITION => TB("Take a position in the debate"),
            BflFieldCatalog.GOAL_RAISE_AWARENESS => TB("Raise awareness"),
            BflFieldCatalog.GOAL_SHOW_OPTIONS => TB("Show options for action"),
            BflFieldCatalog.GOAL_INFLUENCE_POLICY => TB("Influence policy"),

            BflFieldCatalog.QUESTIONS_MIXED => TB("Mixed"),
            BflFieldCatalog.QUESTIONS_SCIENTIFIC_CRITICISM => TB("Scientific criticism"),
            BflFieldCatalog.QUESTIONS_EMOTIONAL_CRITICISM => TB("Emotionally charged criticism"),
            BflFieldCatalog.QUESTIONS_MANIPULATIVE => TB("Manipulative questions and straw men"),
            BflFieldCatalog.QUESTIONS_POLITICAL_INSTRUMENTALIZATION => TB("Political instrumentalization"),

            BflFieldCatalog.DIFFICULTY_MODERATE => TB("Moderate"),
            BflFieldCatalog.DIFFICULTY_DEMANDING => TB("Demanding"),
            BflFieldCatalog.DIFFICULTY_VERY_DEMANDING => TB("Very demanding"),

            _ => string.Empty,
        };
    }
}