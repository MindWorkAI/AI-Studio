using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools;

public static class AssistantCategoryExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(AssistantCategoryExtensions).Namespace, nameof(AssistantCategoryExtensions));
    
    public static string Name(this AssistantCategory category) => category switch
    {
        AssistantCategory.AS_IS => TB("Please select the assistant category"),
        AssistantCategory.GENERAL => TB("General"),
        AssistantCategory.SCIENTIFIC => TB("Scientific"),
        AssistantCategory.BUSINESS => TB("Business"),
        AssistantCategory.PRODUCTIVITY => TB("Productivity"),
        AssistantCategory.DEVELOPMENT => TB("Software Development"),
        AssistantCategory.LEARNING => TB("Learning"),
        AssistantCategory.AI_STUDIO => TB("AI Studio Development"),
        AssistantCategory.OTHER => TB("Other"),

        _ => string.Empty,
    };
    
    public static string NameSelecting(this AssistantCategory category)
    {
        if(category is AssistantCategory.AS_IS)
            return TB("Please select the assistant category");

        return category.Name();
    }
}
