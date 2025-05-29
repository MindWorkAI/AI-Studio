namespace AIStudio.Tools.PluginSystem;

public static class PluginCategoryExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(PluginCategoryExtensions).Namespace, nameof(PluginCategoryExtensions));
    
    public static string GetName(this PluginCategory type) => type switch
    {
        PluginCategory.NONE => TB("None"),
        PluginCategory.CORE => TB("AI Studio Core"),
        
        PluginCategory.BUSINESS => TB("Business"),
        PluginCategory.INDUSTRY => TB("Industry"),
        PluginCategory.UTILITY => TB("Utility"),
        PluginCategory.SOFTWARE_DEVELOPMENT => TB("Software Development"),
        PluginCategory.GAMING => TB("Gaming"),
        PluginCategory.EDUCATION => TB("Education"),
        PluginCategory.ENTERTAINMENT => TB("Entertainment"),
        PluginCategory.SOCIAL => TB("Social"),
        PluginCategory.SHOPPING => TB("Shopping"),
        PluginCategory.TRAVEL => TB("Travel"),
        PluginCategory.HEALTH => TB("Health"),
        PluginCategory.FITNESS => TB("Fitness"),
        PluginCategory.FOOD => TB("Food"),
        PluginCategory.PARTY => TB("Party"),
        PluginCategory.SPORTS => TB("Sports"),
        PluginCategory.NEWS => TB("News"),
        PluginCategory.WEATHER => TB("Weather"),
        PluginCategory.MUSIC => TB("Music"),
        PluginCategory.POLITICAL => TB("Political"),
        PluginCategory.SCIENCE => TB("Science"),
        PluginCategory.TECHNOLOGY => TB("Technology"),
        PluginCategory.ART => TB("Art"),
        PluginCategory.FICTION => TB("Fiction"),
        PluginCategory.WRITING => TB("Writing"),
        PluginCategory.CONTENT_CREATION => TB("Content Creation"),
        
        _ => TB("Unknown plugin category"),
    };
}