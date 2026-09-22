using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.Security;

public static class PromptInjectionSourceKindExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(PromptInjectionSourceKindExtensions).Namespace, nameof(PromptInjectionSourceKindExtensions));

    public static string GetDisplayName(this PromptInjectionSourceKind kind) => kind switch
    {
        PromptInjectionSourceKind.WEB_CONTENT => TB("Web content"),
        PromptInjectionSourceKind.FILE_CONTENT => TB("File content"),
        PromptInjectionSourceKind.CHAT_ATTACHMENT => TB("Chat attachment"),
        PromptInjectionSourceKind.RETRIEVAL_CONTEXT => TB("Retrieved context"),
        _ => TB("Unknown"),
    };
}