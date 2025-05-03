using AIStudio.Tools.PluginSystem;

namespace AIStudio.Settings.DataModel;

public static class LangBehaviorExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(LangBehaviorExtensions).Namespace, nameof(LangBehaviorExtensions));
    
    public static string Name(this LangBehavior langBehavior) => langBehavior switch
    {
        LangBehavior.AUTO => TB("Choose the language automatically, based on your system language."),
        LangBehavior.MANUAL => TB("Choose the language manually."),

        _ => TB("Unknown option")
    };
}