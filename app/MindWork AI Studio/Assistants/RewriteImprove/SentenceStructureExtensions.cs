namespace AIStudio.Assistants.RewriteImprove;

public static class SentenceStructureExtensions
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(SentenceStructureExtensions).Namespace, nameof(SentenceStructureExtensions));
    
    public static string Name(this SentenceStructure sentenceStructure) => sentenceStructure switch
    {
        SentenceStructure.ACTIVE => TB("Active voice"),
        SentenceStructure.PASSIVE => TB("Passive voice"),
        
        _ => TB("Not Specified"),
    };

    public static string Prompt(this SentenceStructure sentenceStructure) => sentenceStructure switch
    {
        SentenceStructure.ACTIVE => " Use an active voice for the sentence structure.",
        SentenceStructure.PASSIVE => " Use a passive voice for the sentence structure.",
        
        _ => string.Empty,
    };
}