namespace AIStudio.Assistants.SWOTAnalysis;

internal static class SwotAnalysisCategoryExtensions
{
    /// <summary>
    /// The name of the category in the JSON the models read and write, e.g., "strengths".
    /// </summary>
    public static string JsonName(this SwotAnalysisCategory category) => category switch
    {
        SwotAnalysisCategory.STRENGTHS => "strengths",
        SwotAnalysisCategory.WEAKNESSES => "weaknesses",
        SwotAnalysisCategory.OPPORTUNITIES => "opportunities",
        SwotAnalysisCategory.THREATS => "threats",

        _ => string.Empty,
    };

    /// <summary>
    /// The letter the factor IDs of the category start with, e.g., S1 for the first strength.
    /// </summary>
    public static string FactorIdPrefix(this SwotAnalysisCategory category) => category switch
    {
        SwotAnalysisCategory.STRENGTHS => "S",
        SwotAnalysisCategory.WEAKNESSES => "W",
        SwotAnalysisCategory.OPPORTUNITIES => "O",
        SwotAnalysisCategory.THREATS => "T",

        _ => string.Empty,
    };
}