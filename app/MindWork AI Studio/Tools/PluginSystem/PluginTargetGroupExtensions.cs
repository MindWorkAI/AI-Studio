namespace AIStudio.Tools.PluginSystem;

public static class PluginTargetGroupExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(PluginTargetGroupExtensions).Namespace, nameof(PluginTargetGroupExtensions));
    
    public static string Name(this PluginTargetGroup group) => group switch
    {
        PluginTargetGroup.NONE => TB("No target group"),

        PluginTargetGroup.EVERYONE => TB("Everyone"),
        PluginTargetGroup.CHILDREN => TB("Children"),
        PluginTargetGroup.TEENAGERS => TB("Teenagers"),
        PluginTargetGroup.STUDENTS => TB("Students"),
        PluginTargetGroup.ADULTS => TB("Adults"),

        PluginTargetGroup.INDUSTRIAL_WORKERS => TB("Industrial workers"),
        PluginTargetGroup.OFFICE_WORKERS => TB("Office workers"),
        PluginTargetGroup.BUSINESS_PROFESSIONALS => TB("Business professionals"),
        PluginTargetGroup.SOFTWARE_DEVELOPERS => TB("Software developers"),
        PluginTargetGroup.SCIENTISTS => TB("Scientists"),
        PluginTargetGroup.TEACHERS => TB("Teachers"),
        PluginTargetGroup.ARTISTS => TB("Artists"),

        _ => TB("Unknown target group"),
    };
}