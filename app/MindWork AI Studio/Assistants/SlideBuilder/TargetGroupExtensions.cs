namespace AIStudio.Assistants.SlideBuilder;

public static class TargetGroupExtensions
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(TargetGroupExtensions).Namespace, nameof(TargetGroupExtensions));

    public static string Name(this TargetGroup group) => group switch
    {
        TargetGroup.NO_CHANGE => TB("No target group"),

        TargetGroup.CHILDREN => TB("Children"),
        TargetGroup.STUDENTS => TB("Students"),
        TargetGroup.SCIENTISTS => TB("Scientists"),
        TargetGroup.OFFICE_WORKERS => TB("Office workers"),
        TargetGroup.MANAGEMENT_BOARD => TB("Executive committee"),

        _ => TB("No target group"),
    };

    public static string Prompt(this TargetGroup group) => group switch
    {
        TargetGroup.NO_CHANGE => "Do not tailor the text to a specific target group.",

        TargetGroup.CHILDREN => "Write for children. Keep the language simple and concrete.",
        TargetGroup.STUDENTS => "Write for students. Keep it structured and easy to study.",
        TargetGroup.SCIENTISTS => "Use precise, technical language. Structure logically with clear methods/results.",
        TargetGroup.OFFICE_WORKERS => "Be clear, practical, and concise. Use bullet points. Focus on action.",
        TargetGroup.MANAGEMENT_BOARD => "Focus on strategy, ROI, risks. Summarize. Recommend decisions.",

        _ => "Do not tailor the text to a specific target group.",
    };
}
