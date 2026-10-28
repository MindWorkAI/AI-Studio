namespace AIStudio.Assistants.SWOTAnalysis;

internal static class SwotAnalysisPromptInput
{
    public static string BuildSections(
        string analysisSubject,
        string analysisGoal,
        string analysisFocus,
        SwotContextualKnowledge contextualKnowledge,
        string sharedResearch = "") =>
        $"""
         <analysis-subject>
         {analysisSubject}
         </analysis-subject>

         <analysis-goal>
         {analysisGoal}
         </analysis-goal>

         <analysis-focus>
         {FocusOrDefault(analysisFocus)}
         </analysis-focus>

         <contextual-knowledge>
         {contextualKnowledge.Describe()}
         </contextual-knowledge>
         {SharedResearchSection(sharedResearch)}
         """;

    /// <summary>
    /// The analysis focus, or what stands in for a focus nobody gave.
    /// </summary>
    public static string FocusOrDefault(string analysisFocus) => string.IsNullOrWhiteSpace(analysisFocus)
        ? "No additional analysis focus was provided."
        : analysisFocus;

    /// <summary>
    /// The shared-research section, which follows the other sections; empty without research.
    /// </summary>
    public static string SharedResearchSection(string sharedResearch) => string.IsNullOrWhiteSpace(sharedResearch)
        ? string.Empty
        : $"""

           <shared-research>
           {sharedResearch.Trim()}
           </shared-research>
           """;
}