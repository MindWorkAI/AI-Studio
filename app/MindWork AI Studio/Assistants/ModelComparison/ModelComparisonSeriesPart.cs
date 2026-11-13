namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// A part of the setup of a comparison series, e.g., one which a new series cannot take over from an existing one.
/// </summary>
public enum ModelComparisonSeriesPart
{
    FIRST_MODEL,
    SECOND_MODEL,
    JUDGE,
    PROFILE,
    TOOLS,
    DATA_SOURCES,
}