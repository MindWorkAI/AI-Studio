namespace AIStudio.Assistants.SWOTAnalysis;

internal static class SwotAnalysisPromptInput
{
    public static string BuildSections(
        string analysisSubject,
        string analysisGoal,
        string analysisFocus,
        SwotContextualKnowledge contextualKnowledge,
        string sharedResearch = "")
    {
        var effectiveAnalysisFocus = string.IsNullOrWhiteSpace(analysisFocus)
            ? "No additional analysis focus was provided."
            : analysisFocus;
        var sharedResearchSection = string.IsNullOrWhiteSpace(sharedResearch)
            ? string.Empty
            : $"""

               <shared-research>
               {sharedResearch.Trim()}
               </shared-research>
               """;

        return $"""
                <analysis-subject>
                {analysisSubject}
                </analysis-subject>

                <analysis-goal>
                {analysisGoal}
                </analysis-goal>

                <analysis-focus>
                {effectiveAnalysisFocus}
                </analysis-focus>

                <contextual-knowledge>
                {contextualKnowledge.Describe()}
                </contextual-knowledge>
                {sharedResearchSection}
                """;
    }
}