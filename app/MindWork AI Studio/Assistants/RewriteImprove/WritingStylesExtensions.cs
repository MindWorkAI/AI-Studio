namespace AIStudio.Assistants.RewriteImprove;

public static class WritingStylesExtensions
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(WritingStylesExtensions).Namespace, nameof(WritingStylesExtensions));
    
    public static string Name(this WritingStyles style) => style switch
    {
        WritingStyles.EVERYDAY => TB("Everyday (personal texts, social media)"),
        WritingStyles.BUSINESS => TB("Business (business emails, reports, presentations)"),
        WritingStyles.SCIENTIFIC => TB("Scientific (scientific papers, research reports)"),
        WritingStyles.JOURNALISTIC => TB("Journalistic (magazines, newspapers, news)"),
        WritingStyles.LITERARY => TB("Literary (fiction, poetry)"),
        WritingStyles.TECHNICAL => TB("Technical (manuals, documentation)"),
        WritingStyles.MARKETING => TB("Marketing (advertisements, sales texts)"),
        WritingStyles.ACADEMIC => TB("Academic (essays, seminar papers)"),
        WritingStyles.LEGAL => TB("Legal (legal texts, contracts)"),
        WritingStyles.CHANGELOG => TB("Changelog (release notes, version history)"),
        
        _ => TB("Not specified"),
    };

    public static string Prompt(this WritingStyles style) => style switch
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
        WritingStyles.CHANGELOG => "Use a changelog style like for release notes, version history, and software updates. Most important is clarity and conciseness. The changelog is structured as a Markdown list. Most list items start with one of the following verbs: Added, Changed, Deprecated, Removed, Fixed, Refactored, Improved, or Upgraded -- these verbs should also translated to the target language. Also, changelogs use past tense.",
        
        _ => "Keep the style of the text as it is.",
    };
}