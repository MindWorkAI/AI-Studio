namespace AIStudio.Assistants.SWOTAnalysis;

internal static class SwotAnalysisPromptInput
{
    public static string BuildSections(
        string analysisSubject,
        string analysisGoal,
        string analysisFocus,
        bool hasContextualKnowledge,
        string sharedResearch = "")
    {
        var effectiveAnalysisFocus = string.IsNullOrWhiteSpace(analysisFocus)
            ? "No additional analysis focus was provided."
            : analysisFocus;
        var contextualKnowledgeDescription = hasContextualKnowledge
            ? "The files attached to this message are contextual knowledge for the analysis."
            : "No additional contextual knowledge files were provided.";
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
                {contextualKnowledgeDescription}
                </contextual-knowledge>
                {sharedResearchSection}
                """;
    }
}