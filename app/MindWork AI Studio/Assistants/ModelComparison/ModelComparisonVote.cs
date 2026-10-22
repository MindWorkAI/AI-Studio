namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// What the voter chose while the answers were still shown without the names of the models.
/// </summary>
/// <remarks>
/// This names a column on the screen, not a model. Which model stood in a column is what
/// <see cref="ModelComparisonPresentationOrder"/> says.
/// </remarks>
public enum ModelComparisonVote
{
    COLUMN_A,
    COLUMN_B,
    TIE,
}