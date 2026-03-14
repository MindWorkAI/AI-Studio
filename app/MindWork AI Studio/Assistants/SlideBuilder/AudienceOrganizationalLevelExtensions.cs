namespace AIStudio.Assistants.SlideBuilder;

public static class AudienceOrganizationalLevelExtensions
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(AudienceOrganizationalLevelExtensions).Namespace, nameof(AudienceOrganizationalLevelExtensions));

    public static string Name(this AudienceOrganizationalLevel level) => level switch
    {
        AudienceOrganizationalLevel.UNSPECIFIED => TB("Unspecified organizational level"),
        AudienceOrganizationalLevel.TRAINEES => TB("Trainees"),
        AudienceOrganizationalLevel.INDIVIDUAL_CONTRIBUTORS => TB("Individual contributors"),
        AudienceOrganizationalLevel.TEAM_LEADS => TB("Team leads"),
        AudienceOrganizationalLevel.MANAGERS => TB("Managers"),
        AudienceOrganizationalLevel.EXECUTIVES => TB("Executives"),
        AudienceOrganizationalLevel.BOARD_MEMBERS => TB("Board members"),
        
        _ => TB("Unspecified organizational level"),
    };

    public static string Prompt(this AudienceOrganizationalLevel level) => level switch
    {
        AudienceOrganizationalLevel.UNSPECIFIED => "Do not tailor the text to a specific organizational level.",
        AudienceOrganizationalLevel.TRAINEES => "Keep the content supportive and introductory. Explain context and avoid assuming prior organizational knowledge.",
        AudienceOrganizationalLevel.INDIVIDUAL_CONTRIBUTORS => "Focus on execution, clarity, responsibilities, and practical next steps.",
        AudienceOrganizationalLevel.TEAM_LEADS => "Focus on coordination, tradeoffs, risks, and concrete actions for a small team.",
        AudienceOrganizationalLevel.MANAGERS => "Focus on planning, priorities, outcomes, risks, and resource implications.",
        AudienceOrganizationalLevel.EXECUTIVES => "Focus on strategy, business impact, risks, and the decisions required.",
        AudienceOrganizationalLevel.BOARD_MEMBERS => "Provide a concise executive-level summary with governance, strategy, risk, and decision relevance.",
        
        _ => "Do not tailor the text to a specific organizational level.",
    };
}