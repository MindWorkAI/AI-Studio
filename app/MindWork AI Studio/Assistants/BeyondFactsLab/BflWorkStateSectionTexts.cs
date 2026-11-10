namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// The titles of the sections of the work states of the BeyondFacts Lab, in the language of the app.
/// </summary>
public static class BflWorkStateSectionTexts
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(BflWorkStateSectionTexts).Namespace, nameof(BflWorkStateSectionTexts));

    /// <param name="section">The section.</param>
    extension(BflWorkStateSection section)
    {
        /// <summary>
        /// Gets the title of the section.
        /// </summary>
        public string Title => (section.Step, section.Key) switch
        {
            (_, BflWorkStateSections.OPEN_POINTS) => TB("Open points"),

            (BflStep.FRAMEWORK, "FORMAT") => TB("Format and setting"),
            (BflStep.FRAMEWORK, "AUDIENCE") => TB("Audience"),
            (BflStep.FRAMEWORK, "SPEAKING_TIME") => TB("Duration and speaking time"),
            (BflStep.FRAMEWORK, "PARTICIPANTS") => TB("Other participants"),
            (BflStep.FRAMEWORK, "CONFLICTS") => TB("Possible lines of conflict"),

            (BflStep.KNOWLEDGE, "ESTABLISHED") => TB("Established findings"),
            (BflStep.KNOWLEDGE, "UNCERTAINTIES") => TB("Uncertainties and limits"),
            (BflStep.KNOWLEDGE, "WEAK_POINTS") => TB("Weak points and possible criticism"),
            (BflStep.KNOWLEDGE, "CONTROVERSIES") => TB("Controversies in the field"),
            (BflStep.KNOWLEDGE, "EXPERTISE_LIMITS") => TB("Limits of the expertise"),
            (BflStep.KNOWLEDGE, "KEY_MESSAGES") => TB("Possible key messages"),

            (BflStep.GOALS_AND_ROLE, "GOAL") => TB("Goal of the contribution"),
            (BflStep.GOALS_AND_ROLE, "ROLE") => TB("Role"),
            (BflStep.GOALS_AND_ROLE, "REASONS") => TB("Reasons"),
            (BflStep.GOALS_AND_ROLE, "CONSEQUENCES") => TB("Consequences for the appearance"),

            (BflStep.VALUES_AND_PERSPECTIVES, "VALUES") => TB("Important values"),
            (BflStep.VALUES_AND_PERSPECTIVES, "VALUE_CONFLICTS") => TB("Value conflicts and trade-offs"),
            (BflStep.VALUES_AND_PERSPECTIVES, "PERSONAL_INVOLVEMENT") => TB("Personal involvement"),
            (BflStep.VALUES_AND_PERSPECTIVES, "PERSPECTIVES") => TB("Perspectives considered"),
            (BflStep.VALUES_AND_PERSPECTIVES, "CONSEQUENCES") => TB("Consequences for position and communication"),

            (BflStep.STRATEGY, "KEY_MESSAGES") => TB("Key messages"),
            (BflStep.STRATEGY, "STRUCTURE") => TB("Opening and structure"),
            (BflStep.STRATEGY, "LANGUAGE") => TB("Language"),
            (BflStep.STRATEGY, "UNCERTAINTY") => TB("Making uncertainty visible"),
            (BflStep.STRATEGY, "FRAMING") => TB("Framing"),
            (BflStep.STRATEGY, "PARTICIPANTS") => TB("Participants and counterparts"),
            (BflStep.STRATEGY, "COUNTERARGUMENTS") => TB("Counterarguments"),
            (BflStep.STRATEGY, "WORRIES") => TB("Dealing with worries"),

            (BflStep.TEST_SITUATION, "STRENGTHS") => TB("What already works well"),
            (BflStep.TEST_SITUATION, "PRACTICE") => TB("What to practice further"),
            (BflStep.TEST_SITUATION, "HARD_QUESTIONS") => TB("Hardest questions and approaches to answers"),
            (BflStep.TEST_SITUATION, "POSITION") => TB("Changes in position"),

            (BflStep.RESULTS, "CHANGED") => TB("Changed"),
            (BflStep.RESULTS, "KEPT") => TB("Kept"),
            (BflStep.RESULTS, "LEFT_OPEN") => TB("Left open"),
            (BflStep.RESULTS, "NEXT_STEPS") => TB("Next steps"),

            _ => section.Key,
        };
    }
}