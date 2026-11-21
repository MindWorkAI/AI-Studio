namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// Which model a column-based choice -- a vote or a judge verdict -- actually means, once translated
/// out of the run's own presentation order.
/// </summary>
/// <remarks>
/// First/Second names the models the way the user picked them, the one thing that stays comparable
/// across every run of a batch: the column a model stood in is drawn fresh for every run, so a tally
/// kept in column terms would count nothing meaningful.
/// </remarks>
public enum ModelComparisonModelChoice
{
    FIRST,
    SECOND,
    TIE,
}
