namespace AIStudio.Tools.PluginSystem;

public static class PluginTargetGroupExtensions
{
    public static string Name(this PluginTargetGroup group) => group switch
    {
        PluginTargetGroup.NONE => "No target group",

        PluginTargetGroup.EVERYONE => "Everyone",
        PluginTargetGroup.CHILDREN => "Children",
        PluginTargetGroup.TEENAGERS => "Teenagers",
        PluginTargetGroup.STUDENTS => "Students",
        PluginTargetGroup.ADULTS => "Adults",

        PluginTargetGroup.INDUSTRIAL_WORKERS => "Industrial workers",
        PluginTargetGroup.OFFICE_WORKERS => "Office workers",
        PluginTargetGroup.BUSINESS_PROFESSIONALS => "Business professionals",
        PluginTargetGroup.SOFTWARE_DEVELOPERS => "Software developers",
        PluginTargetGroup.SCIENTISTS => "Scientists",
        PluginTargetGroup.TEACHERS => "Teachers",
        PluginTargetGroup.ARTISTS => "Artists",

        _ => "Unknown target group",
    };
}