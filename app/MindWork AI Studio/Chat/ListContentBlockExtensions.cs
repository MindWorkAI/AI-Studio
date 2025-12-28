using AIStudio.Provider;

namespace AIStudio.Chat;

public static class ListContentBlockExtensions
{
    /// <summary>
    /// Processes a list of content blocks by transforming them into a collection of message results asynchronously.
    /// </summary>
    /// <param name="blocks">The list of content blocks to process.</param>
    /// <param name="transformer">A function that transforms each content block into a message result asynchronously.</param>
    /// <returns>An asynchronous task that resolves to a list of transformed results.</returns>
    public static async Task<IList<IMessageBase>> BuildMessages(this List<ContentBlock> blocks, Func<ContentBlock, Task<IMessageBase>> transformer)
    {
        var messages = blocks
            .Where(n => n.ContentType is ContentType.TEXT && !string.IsNullOrWhiteSpace((n.Content as ContentText)?.Text))
            .Select(transformer)
            .ToList();
        
        // Await all messages:
        await Task.WhenAll(messages);

        // Select all results:
        return messages.Select(n => n.Result).ToList();
    }
}