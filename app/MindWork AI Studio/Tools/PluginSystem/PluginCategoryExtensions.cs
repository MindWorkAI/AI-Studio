namespace AIStudio.Tools.PluginSystem;

public static class PluginCategoryExtensions
{
    public static string GetName(this PluginCategory type) => type switch
    {
        PluginCategory.NONE => "None",
        PluginCategory.CORE => "AI Studio Core",
        
        PluginCategory.BUSINESS => "Business",
        PluginCategory.INDUSTRY => "Industry",
        PluginCategory.UTILITY => "Utility",
        PluginCategory.SOFTWARE_DEVELOPMENT => "Software Development",
        PluginCategory.GAMING => "Gaming",
        PluginCategory.EDUCATION => "Education",
        PluginCategory.ENTERTAINMENT => "Entertainment",
        PluginCategory.SOCIAL => "Social",
        PluginCategory.SHOPPING => "Shopping",
        PluginCategory.TRAVEL => "Travel",
        PluginCategory.HEALTH => "Health",
        PluginCategory.FITNESS => "Fitness",
        PluginCategory.FOOD => "Food",
        PluginCategory.PARTY => "Party",
        PluginCategory.SPORTS => "Sports",
        PluginCategory.NEWS => "News",
        PluginCategory.WEATHER => "Weather",
        PluginCategory.MUSIC => "Music",
        PluginCategory.POLITICAL => "Political",
        PluginCategory.SCIENCE => "Science",
        PluginCategory.TECHNOLOGY => "Technology",
        PluginCategory.ART => "Art",
        PluginCategory.FICTION => "Fiction",
        PluginCategory.WRITING => "Writing",
        PluginCategory.CONTENT_CREATION => "Content Creation",
        
        _ => "Unknown plugin category",
    };
}