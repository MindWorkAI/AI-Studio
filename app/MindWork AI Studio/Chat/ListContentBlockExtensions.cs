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
    public static async Task<IList<IMessageBase>> BuildMessagesAsync(this List<ContentBlock> blocks, Func<ChatRole, string> roleTransformer)
    {
        var messageTaskList = new List<Task<IMessageBase>>(blocks.Count);
        foreach (var block in blocks)
        {
            switch (block.Content)
            {
                case ContentText text when block.ContentType is ContentType.TEXT && !string.IsNullOrWhiteSpace(text.Text):
                    messageTaskList.Add(CreateTextMessageAsync(block, text));
                    break;
            }
        }

        // Await all messages:
        await Task.WhenAll(messageTaskList);
        
        // Select all results:
        return messageTaskList.Select(n => n.Result).ToList();
        
        // Local function to create a text message asynchronously.
        Task<IMessageBase> CreateTextMessageAsync(ContentBlock block, ContentText text)
        {
            return Task.Run(async () => new TextMessage
            {
                Role = roleTransformer(block.Role),
                Content = await text.PrepareTextContentForAI(),
            } as IMessageBase);
        }
    }
    
    /// <summary>
    /// Processes a list of content blocks using standard role transformations to create message results asynchronously.
    /// </summary>
    /// <param name="blocks">The list of content blocks to process.</param>
    /// <returns>>An asynchronous task that resolves to a list of transformed message results.</returns>
    public static async Task<IList<IMessageBase>> BuildMessagesUsingStandardRolesAsync(this List<ContentBlock> blocks) => await blocks.BuildMessagesAsync(StandardRoleTransformer);

    private static string StandardRoleTransformer(ChatRole role) => role switch
    {
        ChatRole.USER => "user",
        ChatRole.AI => "assistant",
        ChatRole.AGENT => "assistant",
        ChatRole.SYSTEM => "system",

        _ => "user",
    };
}