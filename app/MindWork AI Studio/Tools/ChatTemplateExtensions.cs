using AIStudio.Settings;

namespace AIStudio.Tools;

public static class ChatTemplateExtensions
{
    public static IEnumerable<ChatTemplate> GetAllChatTemplates(this IEnumerable<ChatTemplate> chatTemplates)
    {
        yield return ChatTemplate.NO_CHAT_TEMPLATE;
        foreach (var chatTemplate in chatTemplates)
            yield return chatTemplate;
    }

    /// <summary>
    /// The templates in the order the settings show them: the user's own by name, then those of the
    /// organization by name.
    /// </summary>
    /// <remarks>
    /// The preview of the templates shows the start of this list, see <c>ChatTemplatesSection</c>, so it
    /// needs no reason of its own for the templates it picks: they are the first ones on the page.
    /// </remarks>
    public static IEnumerable<ChatTemplate> SortedForSettings(this IEnumerable<ChatTemplate> chatTemplates) => chatTemplates
        .OrderBy(chatTemplate => chatTemplate.IsEnterpriseConfiguration)
        .ThenBy(chatTemplate => chatTemplate.GetSafeName(), StringComparer.CurrentCultureIgnoreCase);
}