namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// The steps of a preparation in the BeyondFacts Lab, in the order the lab suggests.
/// </summary>
/// <remarks>
/// The names are stored, e.g., as the step shown last, so they never change. People may jump to any step.
/// </remarks>
public enum BflStep
{
    /// <summary>
    /// What the event is about, for whom, and what the person was asked to contribute.
    /// </summary>
    FRAMEWORK,

    /// <summary>
    /// What the person knows for sure, what is uncertain, and where their expertise ends.
    /// </summary>
    KNOWLEDGE,

    /// <summary>
    /// What the person wants to achieve, and in which role they speak.
    /// </summary>
    GOALS_AND_ROLE,

    /// <summary>
    /// What matters to the person, what touches them, and what moves others.
    /// </summary>
    VALUES_AND_PERSPECTIVES,

    /// <summary>
    /// How the person conveys evidence, trade-offs between values, and open questions.
    /// </summary>
    STRATEGY,

    /// <summary>
    /// Critical questions and opposing positions, played through.
    /// </summary>
    TEST_SITUATION,

    /// <summary>
    /// What the person takes away from the preparation, and the materials for the event.
    /// </summary>
    RESULTS,
}