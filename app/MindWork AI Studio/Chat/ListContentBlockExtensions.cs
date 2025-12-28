using AIStudio.Provider;
using AIStudio.Provider.OpenAI;

namespace AIStudio.Chat;

public static class ListContentBlockExtensions
{
    /// <summary>
    /// Processes a list of content blocks by transforming them into a collection of message results asynchronously.
    /// </summary>
    /// <param name="blocks">The list of content blocks to process.</param>
    /// <param name="roleTransformer">A function that transforms each content block into a message result asynchronously.</param>
    /// <returns>An asynchronous task that resolves to a list of transformed results.</returns>
    public static async Task<IList<IMessageBase>> BuildMessages(this List<ContentBlock> blocks, Func<ChatRole, string> roleTransformer)
    {
        var messages = blocks
            .Where(n => n.ContentType is ContentType.TEXT && !string.IsNullOrWhiteSpace((n.Content as ContentText)?.Text))
            .Select(async n => new TextMessage
                {
                    Role = roleTransformer(n.Role),
                    Content = n.Content switch
                    {
                        ContentText text => await text.PrepareTextContentForAI(),
                        _ => string.Empty,
                    }
            })
            .ToList();
        
        // Await all messages:
        await Task.WhenAll(messages);

        // Select all results:
        return messages.Select(n => n.Result).Cast<IMessageBase>().ToList();
    }
    
    /// <summary>
    /// Processes a list of content blocks using standard role transformations to create message results asynchronously.
    /// </summary>
    /// <param name="blocks">The list of content blocks to process.</param>
    /// <returns>>An asynchronous task that resolves to a list of transformed message results.</returns>
    public static async Task<IList<IMessageBase>> BuildMessagesUsingStandardRoles(this List<ContentBlock> blocks) => await blocks.BuildMessages(StandardRoleTransformer);

    private static string StandardRoleTransformer(ChatRole role) => role switch
    {
        ChatRole.USER => "user",
        ChatRole.AI => "assistant",
        ChatRole.AGENT => "assistant",
        ChatRole.SYSTEM => "system",

        _ => "user",
    };
}