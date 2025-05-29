namespace AIStudio.Chat;

public static class ChatRoles
{
    public static IEnumerable<ChatRole> ChatTemplateRoles()
    {
        yield return ChatRole.USER;
        yield return ChatRole.AI;
    }
}