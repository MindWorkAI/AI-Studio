namespace AIStudio.Assistants.RewriteImprove;

public static class SentenceStructureExtensions
{
    public static string Name(this SentenceStructure sentenceStructure) => sentenceStructure switch
    {
        SentenceStructure.ACTIVE => "Active voice",
        SentenceStructure.PASSIVE => "Passive voice",
        
        _ => "Not Specified",
    };

    public static string Prompt(this SentenceStructure sentenceStructure) => sentenceStructure switch
    {
        SentenceStructure.ACTIVE => " Use an active voice for the sentence structure.",
        SentenceStructure.PASSIVE => " Use a passive voice for the sentence structure.",
        
        _ => string.Empty,
    };
}