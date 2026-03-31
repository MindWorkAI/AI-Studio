namespace AIStudio.Assistants.SlideBuilder;

public static class AudienceProfileExtensions
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(AudienceProfileExtensions).Namespace, nameof(AudienceProfileExtensions));

    public static string Name(this AudienceProfile profile) => profile switch
    {
        AudienceProfile.UNSPECIFIED => TB("Unspecified audience profile"),
        AudienceProfile.STUDENTS => TB("Students"),
        AudienceProfile.SCIENTISTS => TB("Scientists"),
        AudienceProfile.LAWYERS => TB("Lawyers"),
        AudienceProfile.INVESTORS => TB("Investors"),
        AudienceProfile.ENGINEERS => TB("Engineers"),
        AudienceProfile.SOFTWARE_DEVELOPERS => TB("Software developers"),
        AudienceProfile.JOURNALISTS => TB("Journalists"),
        AudienceProfile.HEALTHCARE_PROFESSIONALS => TB("Healthcare professionals"),
        AudienceProfile.PUBLIC_OFFICIALS => TB("Public officials"),
        AudienceProfile.BUSINESS_PROFESSIONALS => TB("Business professionals"),
        
        _ => TB("Unspecified audience profile"),
    };

    public static string Prompt(this AudienceProfile profile) => profile switch
    {
        AudienceProfile.UNSPECIFIED => "Do not tailor the text to a specific audience profile.",
        AudienceProfile.STUDENTS => "Write for students. Keep it structured, easy to study, and focused on key takeaways.",
        AudienceProfile.SCIENTISTS => "Use precise, technical language. Structure the content logically and focus on methods, evidence, and results.",
        AudienceProfile.LAWYERS => "Write with precise wording. Emphasize definitions, implications, compliance, and risks.",
        AudienceProfile.INVESTORS => "Focus on market potential, business model, differentiation, traction, risks, and financial upside.",
        AudienceProfile.ENGINEERS => "Be technically precise and practical. Focus on systems, constraints, implementation, and tradeoffs.",
        AudienceProfile.SOFTWARE_DEVELOPERS => "Use concise technical language. Focus on architecture, implementation details, tradeoffs, and maintainability.",
        AudienceProfile.JOURNALISTS => "Be clear, factual, and concise. Highlight the most newsworthy points and explain relevance plainly.",
        AudienceProfile.HEALTHCARE_PROFESSIONALS => "Use accurate professional language. Emphasize outcomes, safety, evidence, and practical implications.",
        AudienceProfile.PUBLIC_OFFICIALS => "Focus on public impact, feasibility, budget, compliance, risks, and implementation in a neutral institutional tone.",
        AudienceProfile.BUSINESS_PROFESSIONALS => "Be clear, practical, and concise. Focus on business relevance, decisions, and next steps.",
        
        _ => "Do not tailor the text to a specific audience profile.",
    };
}