using AIStudio.Provider;
using AIStudio.Provider.OpenAI;
using AIStudio.Settings;

namespace AIStudio.Chat;

public static class ListContentBlockExtensions
{
    /// <summary>
    /// Processes a list of content blocks by transforming them into a collection of message results asynchronously.
    /// </summary>
    /// <param name="blocks">The list of content blocks to process.</param>
    /// <param name="roleTransformer">A function that transforms each content block into a message result asynchronously.</param>
    /// <param name="provider">The configured provider, whose model is being written to.</param>
    /// <param name="textSubContentFactory">A factory function to create text sub-content.</param>
    /// <param name="imageSubContentFactory">A factory function to create image sub-content.</param>
    /// <returns>An asynchronous task that resolves to a list of transformed results.</returns>
    public static async Task<IList<IMessageBase>> BuildMessagesAsync(
        this List<ContentBlock> blocks,
        AIStudio.Settings.Provider provider,
        Func<ChatRole, string> roleTransformer,
        Func<string, ISubContent> textSubContentFactory,
        Func<FileAttachmentImage, Task<ISubContent>> imageSubContentFactory)
    {
        //
        // Asked through the configured provider, so that what a person set in their expert settings
        // counts here too. It did not: this path read the automatic answer alone, so somebody who
        // switched image input on saw it work while attaching the picture and saw it ignored while
        // the message was built -- every chat round and every tool round.
        //
        var canProcessImages = provider.SupportsImageInput();

        var messageTaskList = new List<Task<IMessageBase>>(blocks.Count);
        foreach (var block in blocks)
        {
            switch (block.Content)
            {
                // The prompt may or may not contain image(s), but the provider/model cannot process images.
                // Thus, we treat it as a regular text message.
                case ContentText text when block.ContentType is ContentType.TEXT && !string.IsNullOrWhiteSpace(text.Text) && !canProcessImages:
                    messageTaskList.Add(CreateTextMessageAsync(block, text));
                    break;
                
                // The regular case for text content without images:
                case ContentText text when block.ContentType is ContentType.TEXT && !string.IsNullOrWhiteSpace(text.Text) && !text.FileAttachments.ContainsImages():
                    messageTaskList.Add(CreateTextMessageAsync(block, text));
                    break;
                
                // Text prompt with images as attachments, and the provider/model can process images:
                case ContentText text when block.ContentType is ContentType.TEXT && !string.IsNullOrWhiteSpace(text.Text) && text.FileAttachments.ContainsImages():
                    messageTaskList.Add(CreateMultimodalMessageAsync(block, text, textSubContentFactory, imageSubContentFactory));
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
        
        // Local function to create a multimodal message asynchronously.
        Task<IMessageBase> CreateMultimodalMessageAsync(
            ContentBlock block,
            ContentText text,
            Func<string, ISubContent> innerTextSubContentFactory,
            Func<FileAttachmentImage, Task<ISubContent>> innerImageSubContentFactory)
        {
            return Task.Run(async () =>
            {
                var imagesTasks = text.FileAttachments
                    .Where(x => x is { IsImage: true, Exists: true })
                    .Cast<FileAttachmentImage>()
                    .Select(innerImageSubContentFactory)
                    .ToList();
                
                Task.WaitAll(imagesTasks);
                var images = imagesTasks.Select(t => t.Result).ToList();
                
                return new MultimodalMessage
                {
                    Role = roleTransformer(block.Role),
                    Content =
                    [
                        innerTextSubContentFactory(await text.PrepareTextContentForAI()),
                        ..images,
                    ]
                } as IMessageBase;
            });
        }
    }

    /// <summary>
    /// Processes a list of content blocks using direct image URL format to create message results asynchronously.
    /// </summary>
    /// <param name="blocks">The list of content blocks to process.</param>
    /// <param name="provider">The configured provider, whose model is being written to.</param>
    /// <returns>An asynchronous task that resolves to a list of transformed message results.</returns>
    /// <remarks>
    /// Uses direct image URL format where the image data is placed directly in the image_url field:
    /// <code>
    /// { "type": "image_url", "image_url": "data:image/jpeg;base64,..." }
    /// </code>
    /// This format is used by OpenAI, Mistral, and Ollama.
    /// </remarks>
    public static async Task<IList<IMessageBase>> BuildMessagesUsingDirectImageUrlAsync(
        this List<ContentBlock> blocks,
        AIStudio.Settings.Provider provider) => await blocks.BuildMessagesAsync(
            provider,
            StandardRoleTransformer,
            StandardTextSubContentFactory,
            DirectImageSubContentFactory);

    /// <summary>
    /// Processes a list of content blocks using nested image URL format to create message results asynchronously.
    /// </summary>
    /// <param name="blocks">The list of content blocks to process.</param>
    /// <param name="provider">The configured provider, whose model is being written to.</param>
    /// <returns>An asynchronous task that resolves to a list of transformed message results.</returns>
    /// <remarks>
    /// Uses nested image URL format where the image data is wrapped in an object:
    /// <code>
    /// { "type": "image_url", "image_url": { "url": "data:image/jpeg;base64,..." } }
    /// </code>
    /// This format is used by LM Studio, VLLM, llama.cpp, and other OpenAI-compatible providers.
    /// </remarks>
    public static async Task<IList<IMessageBase>> BuildMessagesUsingNestedImageUrlAsync(
        this List<ContentBlock> blocks,
        AIStudio.Settings.Provider provider) => await blocks.BuildMessagesAsync(
            provider,
            StandardRoleTransformer,
            StandardTextSubContentFactory,
            NestedImageSubContentFactory);

    private static ISubContent StandardTextSubContentFactory(string text) => new SubContentText
    {
        Text = text,
    };

    private static async Task<ISubContent> DirectImageSubContentFactory(FileAttachmentImage attachment) => new SubContentImageUrl
    {
        ImageUrl = await attachment.TryAsBase64() is (true, var base64Content)
            ? $"data:{attachment.DetermineMimeType()};base64,{base64Content}"
            : string.Empty,
    };

    private static async Task<ISubContent> NestedImageSubContentFactory(FileAttachmentImage attachment) => new SubContentImageUrlNested
    {
        ImageUrl = new SubContentImageUrlData
        {
            Url = await attachment.TryAsBase64() is (true, var base64Content)
                ? $"data:{attachment.DetermineMimeType()};base64,{base64Content}"
                : string.Empty,
        },
    };

    private static string StandardRoleTransformer(ChatRole role) => role switch
    {
        ChatRole.USER => "user",
        ChatRole.AI => "assistant",
        ChatRole.AGENT => "assistant",
        ChatRole.SYSTEM => "system",

        _ => "user",
    };
}