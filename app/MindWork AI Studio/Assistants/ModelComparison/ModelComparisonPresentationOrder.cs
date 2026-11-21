namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// Which of the two models is shown first, that is, in column A.
/// </summary>
/// <remarks>
/// The order is drawn at random for every comparison, so that the position of an answer says
/// nothing about the model. The first model is the one the user picked first.
/// </remarks>
public enum ModelComparisonPresentationOrder
{
    FIRST_MODEL_FIRST,
    SECOND_MODEL_FIRST,
}