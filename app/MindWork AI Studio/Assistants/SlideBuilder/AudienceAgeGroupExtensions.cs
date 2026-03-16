namespace AIStudio.Assistants.SlideBuilder;

public static class AudienceAgeGroupExtensions
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(AudienceAgeGroupExtensions).Namespace, nameof(AudienceAgeGroupExtensions));

    public static string Name(this AudienceAgeGroup ageGroup) => ageGroup switch
    {
        AudienceAgeGroup.UNSPECIFIED => TB("Unspecified age group"),
        AudienceAgeGroup.CHILDREN => TB("Children"),
        AudienceAgeGroup.TEENAGERS => TB("Teenagers"),
        AudienceAgeGroup.ADULTS => TB("Adults"),
        
        _ => TB("Unspecified age group"),
    };

    public static string Prompt(this AudienceAgeGroup ageGroup) => ageGroup switch
    {
        AudienceAgeGroup.UNSPECIFIED => "Do not tailor the text to a specific age group.",
        AudienceAgeGroup.CHILDREN => "Use simple, concrete language with short sentences and minimal jargon.",
        AudienceAgeGroup.TEENAGERS => "Use clear, approachable language with relatable examples and limited jargon.",
        AudienceAgeGroup.ADULTS => "Use adult-appropriate language with clear structure and direct explanations.",
        
        _ => "Do not tailor the text to a specific age group.",
    };
}