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
}