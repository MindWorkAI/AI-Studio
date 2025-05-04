namespace AIStudio.Assistants.TextSummarizer;

public static class ComplexityExtensions
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(ComplexityExtensions).Namespace, nameof(ComplexityExtensions));
    
    public static string Name(this Complexity complexity) => complexity switch
    {
        Complexity.NO_CHANGE => TB("No change in complexity"),
        
        Complexity.SIMPLE_LANGUAGE => TB("Simple language, e.g., for children"),
        Complexity.TEEN_LANGUAGE => TB("Teen language, e.g., for teenagers"),
        Complexity.EVERYDAY_LANGUAGE => TB("Everyday language, e.g., for adults"),
        Complexity.POPULAR_SCIENCE_LANGUAGE => TB("Popular science language, e.g., for people interested in science"),
        Complexity.SCIENTIFIC_LANGUAGE_FIELD_EXPERTS => TB("Scientific language for experts in this field"),
        Complexity.SCIENTIFIC_LANGUAGE_OTHER_EXPERTS => TB("Scientific language for experts from other fields (interdisciplinary)"),
        
        _ => TB("No change in complexity"),
    };
    
    public static string Prompt(this Complexity complexity, string expertInField) => complexity switch
    {
        Complexity.NO_CHANGE => "Do not change the complexity of the text.",
        
        Complexity.SIMPLE_LANGUAGE => "Simplify the language, e.g., for 8 to 12-year-old children. Might use short sentences and simple words. You could use analogies to explain complex terms.",
        Complexity.TEEN_LANGUAGE => "Use a language suitable for teenagers, e.g., 16 to 19 years old. Might use teenage slang and analogies to explain complex terms.",
        Complexity.EVERYDAY_LANGUAGE => "Use everyday language suitable for adults. Avoid specific scientific terms. Use everday analogies to explain complex terms.",
        Complexity.POPULAR_SCIENCE_LANGUAGE => "Use popular science language, e.g., for people interested in science. The text should be easy to understand, though. Use analogies to explain complex terms.",
        Complexity.SCIENTIFIC_LANGUAGE_FIELD_EXPERTS => "Use scientific language for experts in the field of the texts subject. Use specific terms for this field.",
        Complexity.SCIENTIFIC_LANGUAGE_OTHER_EXPERTS => $"The reader is an expert in {expertInField}. Change the language so that it is suitable. Explain specific terms, so that the reader within his field can understand the text. You might use analogies to explain complex terms.",
        
        _ => "Do not change the complexity of the text.",
    };
}