using AIStudio.Settings;

namespace AIStudio.Tools;

public static class ChatTemplateExtensions
{
    extension(IEnumerable<ChatTemplate> chatTemplates)
    {
        public IEnumerable<ChatTemplate> GetAllChatTemplates()
        {
            yield return ChatTemplate.NO_CHAT_TEMPLATE;
            foreach (var chatTemplate in chatTemplates)
                yield return chatTemplate;
        }
    }
}