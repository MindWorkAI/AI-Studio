namespace AIStudio.Assistants.SlideBuilder;

public static class AudienceExpertiseExtensions
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(AudienceExpertiseExtensions).Namespace, nameof(AudienceExpertiseExtensions));

    public static string Name(this AudienceExpertise expertise) => expertise switch
    {
        AudienceExpertise.UNSPECIFIED => TB("Unspecified expertise"),
        AudienceExpertise.NON_EXPERTS => TB("Non-experts"),
        AudienceExpertise.BASIC => TB("Basic"),
        AudienceExpertise.INTERMEDIATE => TB("Intermediate"),
        AudienceExpertise.EXPERTS => TB("Experts"),
        
        _ => TB("Unspecified expertise"),
    };

    public static string Prompt(this AudienceExpertise expertise) => expertise switch
    {
        AudienceExpertise.UNSPECIFIED => "Do not tailor the text to a specific expertise level.",
        AudienceExpertise.NON_EXPERTS => "Avoid jargon and explain specialized concepts plainly.",
        AudienceExpertise.BASIC => "Use simple terminology and briefly explain important technical terms.",
        AudienceExpertise.INTERMEDIATE => "Assume some familiarity with the topic, but still explain important details clearly.",
        AudienceExpertise.EXPERTS => "Assume deep familiarity with the topic and use precise domain-specific terminology.",
        
        _ => "Do not tailor the text to a specific expertise level.",
    };
}