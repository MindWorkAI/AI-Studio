namespace AIStudio.Assistants.SWOTAnalysis;

internal static class SwotAnalysisRequestBuilder
{
    public static string Build(string analysisSubject, string analysisGoal, string analysisFocus, bool hasContextualKnowledge)
    {
        var effectiveAnalysisFocus = string.IsNullOrWhiteSpace(analysisFocus)
            ? "No additional analysis focus was provided."
            : analysisFocus;
        var contextualKnowledgeDescription = hasContextualKnowledge
            ? "The files attached to this message are contextual knowledge for the analysis."
            : "No additional contextual knowledge files were provided.";

        return $"""
               Create a SWOT analysis using the following inputs according to their assigned roles.

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
               """;
    }
}