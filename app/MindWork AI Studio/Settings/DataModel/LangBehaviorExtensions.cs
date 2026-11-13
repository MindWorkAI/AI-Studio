using AIStudio.Tools.PluginSystem;

namespace AIStudio.Settings.DataModel;

public static class LangBehaviorExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(LangBehaviorExtensions).Namespace, nameof(LangBehaviorExtensions));

    private static string TA(string fallbackEN) => I18N.I.TA(fallbackEN, typeof(LangBehaviorExtensions).Namespace, nameof(LangBehaviorExtensions));
    
    extension(LangBehavior langBehavior)
    {
        public string Name => langBehavior switch
        {
            LangBehavior.AUTO => TA("Choose the language automatically, based on your system language."),
            LangBehavior.MANUAL => TB("Choose the language manually."),

            _ => TB("Unknown option")
        };
    }
}