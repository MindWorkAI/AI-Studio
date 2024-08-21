namespace AIStudio.Assistants.EMail;

public static class WritingStylesExtensions
{
    public static string Name(this WritingStyles style) => style switch
    {
        WritingStyles.ACADEMIC => "Academic",
        WritingStyles.PERSONAL => "Personal",
        WritingStyles.BUSINESS_FORMAL => "Business formal",
        WritingStyles.BUSINESS_INFORMAL => "Business informal",
        
        _ => "Not specified",
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