namespace AIStudio.Settings.DataModel;

public static class LangBehaviorExtensions
{
    public static string Name(this LangBehavior langBehavior) => langBehavior switch
    {
        LangBehavior.AUTO => "Choose the language automatically, based on your system language.",
        LangBehavior.MANUAL => "Choose the language manually.",

        _ => "Unknown option"
    };
}