using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.Security;

public static class PromptInjectionFindingCategoryExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(PromptInjectionFindingCategoryExtensions).Namespace, nameof(PromptInjectionFindingCategoryExtensions));

    public static string GetDisplayName(this PromptInjectionFindingCategory category) => category switch
    {
        PromptInjectionFindingCategory.OVERRIDE => TB("Attempt to override instructions"),
        PromptInjectionFindingCategory.ROLE_OVERRIDE => TB("Attempt to change the AI's role"),
        PromptInjectionFindingCategory.EXFILTRATION => TB("Attempt to expose protected data"),
        PromptInjectionFindingCategory.JAILBREAK => TB("Attempt to bypass safeguards"),
        PromptInjectionFindingCategory.AGENT_MANIPULATION => TB("Attempt to manipulate an agent"),
        PromptInjectionFindingCategory.DELIMITER_EVASION => TB("Hidden instructions using delimiters"),
        PromptInjectionFindingCategory.MARKUP_EVASION => TB("Hidden instructions using markup"),
        PromptInjectionFindingCategory.ENCODING_EVASION => TB("Hidden instructions using encoding"),
        PromptInjectionFindingCategory.PERSISTENCE => TB("Persistent or delayed instruction"),
        PromptInjectionFindingCategory.EVASION => TB("Obfuscated instruction"),
        _ => TB("Unknown"),
    };
}