namespace AIStudio.Assistants.EMail;

public static class WritingStylesExtensions
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(WritingStylesExtensions).Namespace, nameof(WritingStylesExtensions));
    
    public static string Name(this WritingStyles style) => style switch
    {
        WritingStyles.ACADEMIC => TB("Academic"),
        WritingStyles.PERSONAL => TB("Personal"),
        WritingStyles.BUSINESS_FORMAL => TB("Business formal"),
        WritingStyles.BUSINESS_INFORMAL => TB("Business informal"),
        
        _ => TB("Not specified"),
    };
    
    public static string Prompt(this WritingStyles style) => style switch
    {
        WritingStyles.ACADEMIC => "Use an academic style for communication in an academic context like between students and professors.",
        WritingStyles.PERSONAL => "Use a personal style for communication between friends and family.",
        WritingStyles.BUSINESS_FORMAL => "Use a formal business style for this e-mail.",
        WritingStyles.BUSINESS_INFORMAL => "Use an informal business style for this e-mail.",
        
        _ => "Use a formal business style for this e-mail.",
    };
}