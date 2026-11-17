namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Every field of the forms "Your details" in the BeyondFacts Lab.
/// </summary>
/// <remarks>
/// The forms store their values under the IDs of the fields and options, so these IDs never change. A field which
/// is no longer needed keeps its ID reserved. The texts of the fields come from BflFieldTexts.
/// </remarks>
public static class BflFieldCatalog
{
    // Event details:
    public const string FORMAT = "format";
    public const string EVENT_TOPIC = "event_topic";
    public const string AUDIENCE = "audience";
    public const string EVENT_VISIBILITY = "event_visibility";
    public const string CONTRIBUTION_TOPIC = "contribution_topic";
    public const string EVENT_DATE = "event_date";
    public const string EVENT_LOCATION = "event_location";
    public const string EVENT_DURATION = "event_duration";
    public const string CONTRIBUTION_DURATION = "contribution_duration";
    public const string OTHER_PARTICIPANTS = "other_participants";
    public const string EVENT_LINK = "event_link";

    // Knowledge and uncertainty:
    public const string CONTRIBUTION_SUMMARY = "contribution_summary";
    public const string CORE_THESES = "core_theses";
    public const string UNCERTAINTIES = "uncertainties";
    public const string MATERIAL_NOTES = "material_notes";

    // Goals and role:
    public const string GOALS = "goals";
    public const string GOAL_DIRECTIONS = "goal_directions";

    // Values and perspectives:
    public const string VALUES_AND_CONCERNS = "values_and_concerns";
    public const string PERSONAL_CONCERN = "personal_concern";
    public const string OTHER_PERSPECTIVES = "other_perspectives";

    // Strategy:
    public const string COMMUNICATION_WORRIES = "communication_worries";

    // Test situation:
    public const string QUESTION_TYPE = "question_type";
    public const string DIFFICULTY = "difficulty";
    public const string TEST_NOTES = "test_notes";

    // Results and materials:
    public const string POSITION_CHANGES = "position_changes";

    // The options of whether the event is public:
    public const string EVENT_PUBLIC = "event_public";
    public const string EVENT_NOT_PUBLIC = "event_not_public";

    // The options of the directions of a goal:
    public const string GOAL_INFORM = "goal_inform";
    public const string GOAL_MODERATE = "goal_moderate";
    public const string GOAL_TAKE_POSITION = "goal_take_position";
    public const string GOAL_RAISE_AWARENESS = "goal_raise_awareness";
    public const string GOAL_SHOW_OPTIONS = "goal_show_options";
    public const string GOAL_INFLUENCE_POLICY = "goal_influence_policy";

    // The options of the type of questions:
    public const string QUESTIONS_MIXED = "questions_mixed";
    public const string QUESTIONS_SCIENTIFIC_CRITICISM = "questions_scientific_criticism";
    public const string QUESTIONS_EMOTIONAL_CRITICISM = "questions_emotional_criticism";
    public const string QUESTIONS_MANIPULATIVE = "questions_manipulative";
    public const string QUESTIONS_POLITICAL_INSTRUMENTALIZATION = "questions_political_instrumentalization";

    // The options of the difficulty:
    public const string DIFFICULTY_MODERATE = "difficulty_moderate";
    public const string DIFFICULTY_DEMANDING = "difficulty_demanding";
    public const string DIFFICULTY_VERY_DEMANDING = "difficulty_very_demanding";

    /// <summary>
    /// Every field, step by step, in the order the forms show them.
    /// </summary>
    public static readonly IReadOnlyList<BflFieldDefinition> FIELDS =
    [
        new(FORMAT, BflStep.FRAMEWORK, BflFieldKind.SINGLE_LINE) { IsKeyDetail = true, IsSharedWithResearch = true },
        new(EVENT_TOPIC, BflStep.FRAMEWORK, BflFieldKind.SINGLE_LINE) { IsKeyDetail = true, IsSharedWithResearch = true },
        new(AUDIENCE, BflStep.FRAMEWORK, BflFieldKind.SINGLE_LINE) { IsKeyDetail = true },

        // Whether the event is public, which decides whether the lab may research it on the web at all:
        new(EVENT_VISIBILITY, BflStep.FRAMEWORK, BflFieldKind.CHOICE)
        {
            IsKeyDetail = true,
            Options = [new(EVENT_PUBLIC), new(EVENT_NOT_PUBLIC)],
        },

        // The topic of the contribution may be unpublished, so it never reaches the research:
        new(CONTRIBUTION_TOPIC, BflStep.FRAMEWORK, BflFieldKind.MULTI_LINE) { IsKeyDetail = true },

        new(EVENT_DATE, BflStep.FRAMEWORK, BflFieldKind.SINGLE_LINE) { IsSharedWithResearch = true, IdentifiesEvent = true },
        new(EVENT_LOCATION, BflStep.FRAMEWORK, BflFieldKind.SINGLE_LINE) { IsSharedWithResearch = true, IdentifiesEvent = true },
        new(EVENT_DURATION, BflStep.FRAMEWORK, BflFieldKind.SINGLE_LINE),
        new(CONTRIBUTION_DURATION, BflStep.FRAMEWORK, BflFieldKind.SINGLE_LINE),
        new(OTHER_PARTICIPANTS, BflStep.FRAMEWORK, BflFieldKind.MULTI_LINE) { IsSharedWithResearch = true, NameRole = BflNameRole.PARTICIPANTS },
        new(EVENT_LINK, BflStep.FRAMEWORK, BflFieldKind.LINK) { IsSharedWithResearch = true, IdentifiesEvent = true },

        new(CONTRIBUTION_SUMMARY, BflStep.KNOWLEDGE, BflFieldKind.MULTI_LINE) { IsKeyDetail = true },
        new(CORE_THESES, BflStep.KNOWLEDGE, BflFieldKind.MULTI_LINE),
        new(UNCERTAINTIES, BflStep.KNOWLEDGE, BflFieldKind.MULTI_LINE),
        new(MATERIAL_NOTES, BflStep.KNOWLEDGE, BflFieldKind.MULTI_LINE),

        new(GOALS, BflStep.GOALS_AND_ROLE, BflFieldKind.MULTI_LINE),
        new(GOAL_DIRECTIONS, BflStep.GOALS_AND_ROLE, BflFieldKind.MULTI_CHOICE)
        {
            Options = [new(GOAL_INFORM), new(GOAL_MODERATE), new(GOAL_TAKE_POSITION), new(GOAL_RAISE_AWARENESS), new(GOAL_SHOW_OPTIONS), new(GOAL_INFLUENCE_POLICY)],
        },

        new(VALUES_AND_CONCERNS, BflStep.VALUES_AND_PERSPECTIVES, BflFieldKind.MULTI_LINE),
        new(PERSONAL_CONCERN, BflStep.VALUES_AND_PERSPECTIVES, BflFieldKind.MULTI_LINE) { IsPersonalDisclosure = true },
        new(OTHER_PERSPECTIVES, BflStep.VALUES_AND_PERSPECTIVES, BflFieldKind.MULTI_LINE),

        new(COMMUNICATION_WORRIES, BflStep.STRATEGY, BflFieldKind.MULTI_LINE),

        new(QUESTION_TYPE, BflStep.TEST_SITUATION, BflFieldKind.CHOICE)
        {
            Options = [new(QUESTIONS_MIXED), new(QUESTIONS_SCIENTIFIC_CRITICISM), new(QUESTIONS_EMOTIONAL_CRITICISM), new(QUESTIONS_MANIPULATIVE), new(QUESTIONS_POLITICAL_INSTRUMENTALIZATION)],
        },
        new(DIFFICULTY, BflStep.TEST_SITUATION, BflFieldKind.CHOICE)
        {
            Options = [new(DIFFICULTY_MODERATE), new(DIFFICULTY_DEMANDING), new(DIFFICULTY_VERY_DEMANDING)],
        },
        new(TEST_NOTES, BflStep.TEST_SITUATION, BflFieldKind.MULTI_LINE),

        new(POSITION_CHANGES, BflStep.RESULTS, BflFieldKind.MULTI_LINE),
    ];

    /// <summary>
    /// Gets the fields of a step, in the order its form shows them.
    /// </summary>
    /// <param name="step">The step.</param>
    public static IReadOnlyList<BflFieldDefinition> ForStep(BflStep step) => [.. FIELDS.Where(field => field.Step == step)];
}