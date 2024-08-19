namespace AIStudio.Assistants.RewriteImprove;

public static class WritingStylesExtensions
{
    public static string Name(this WritingStyles style)
    {
        return style switch
        {
            WritingStyles.EVERYDAY => "Everyday (personal texts, social media)",
            WritingStyles.BUSINESS => "Business (business emails, reports, presentations)",
            WritingStyles.SCIENTIFIC => "Scientific (scientific papers, research reports)",
            WritingStyles.JOURNALISTIC => "Journalistic (magazines, newspapers, news)",
            WritingStyles.LITERARY => "Literary (fiction, poetry)",
            WritingStyles.TECHNICAL => "Technical (manuals, documentation)",
            WritingStyles.MARKETING => "Marketing (advertisements, sales texts)",
            WritingStyles.ACADEMIC => "Academic (essays, seminar papers)",
            WritingStyles.LEGAL => "Legal (legal texts, contracts)",
            
            _ => "Not specified",
        };
    }
    
    public static string Prompt(this WritingStyles style)
    {
        return style switch
        {
            WritingStyles.EVERYDAY => "Use a everyday style like for personal texts, social media, and informal communication.",
            WritingStyles.BUSINESS => "Use a business style like for business emails, reports, and presentations. Most important is clarity and professionalism.",
            WritingStyles.SCIENTIFIC => "Use a scientific style like for scientific papers, research reports, and academic writing. Most important is precision and objectivity.",
            WritingStyles.JOURNALISTIC => "Use a journalistic style like for magazines, newspapers, and news. Most important is readability and engaging content.",
            WritingStyles.LITERARY => "Use a literary style like for fiction, poetry, and creative writing. Most important is creativity and emotional impact.",
            WritingStyles.TECHNICAL => "Use a technical style like for manuals, documentation, and technical writing. Most important is clarity and precision.",
            WritingStyles.MARKETING => "Use a marketing style like for advertisements, sales texts, and promotional content. Most important is persuasiveness and engagement.",
            WritingStyles.ACADEMIC => "Use a academic style like for essays, seminar papers, and academic writing. Most important is clarity and objectivity.",
            WritingStyles.LEGAL => "Use a legal style like for legal texts, contracts, and official documents. Most important is precision and legal correctness. Use formal legal language.",
            
            _ => "Keep the style of the text as it is.",
        };
    }
}