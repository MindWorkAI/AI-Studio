namespace AIStudio.Tools.PluginSystem;

public enum PluginCategory
{
    NONE,
    CORE,
    
    BUSINESS,
    INDUSTRY,
    UTILITY,
    SOFTWARE_DEVELOPMENT,
    GAMING,
    EDUCATION,
    ENTERTAINMENT,
    SOCIAL,
    SHOPPING,
    TRAVEL,
    HEALTH,
    FITNESS,
    FOOD,
    PARTY,
    SPORTS,
    NEWS,
    WEATHER,
    MUSIC,
    POLITICAL,
    SCIENCE,
    TECHNOLOGY,
    ART,
    FICTION,
    WRITING,
    CONTENT_CREATION,

    /// <summary>
    /// Assistants for everyday tasks, which the assistants page shows under "General".
    /// </summary>
    /// <remarks>
    /// CORE stays the category of configuration and language plugins. An assistant plugin which
    /// names CORE nevertheless appears under "General" as well.
    /// </remarks>
    COMMON,

    /// <summary>
    /// Assistants which help to develop AI Studio itself, e.g., its localization.
    /// </summary>
    AI_STUDIO_DEVELOPMENT,

    /// <summary>
    /// A category the plugin names itself, see CUSTOM_CATEGORIES.
    /// </summary>
    /// <remarks>
    /// Plugins name such categories in the language of their authors, so AI Studio cannot translate them.
    /// </remarks>
    CUSTOM,
}