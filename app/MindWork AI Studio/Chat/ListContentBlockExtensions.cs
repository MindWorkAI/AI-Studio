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
    /// <param name="selectedProvider">The selected LLM provider.</param>
    /// <param name="selectedModel">The selected model.</param>
    /// <param name="textSubContentFactory">A factory function to create text sub-content.</param>
    /// <param name="imageSubContentFactory">A factory function to create image sub-content.</param>
    /// <returns>An asynchronous task that resolves to a list of transformed results.</returns>
    public static async Task<IList<IMessageBase>> BuildMessagesAsync(
        this List<ContentBlock> blocks,
        LLMProviders selectedProvider,
        Model selectedModel,
        Func<ChatRole, string> roleTransformer,
        Func<string, ISubContent> textSubContentFactory,
        Func<FileAttachmentImage, Task<ISubContent>> imageSubContentFactory)
    {
        var capabilities = selectedProvider.GetModelCapabilities(selectedModel);
        var canProcessImages = capabilities.Contains(Capability.MULTIPLE_IMAGE_INPUT) ||
                               capabilities.Contains(Capability.SINGLE_IMAGE_INPUT);
        
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
    /// Processes a list of content blocks using standard role transformations to create message results asynchronously.
    /// </summary>
    /// <param name="blocks">The list of content blocks to process.</param>
    /// <param name="selectedProvider">The selected LLM provider.</param>
    /// <param name="selectedModel">The selected model.</param>
    /// <returns>>An asynchronous task that resolves to a list of transformed message results.</returns>
    public static async Task<IList<IMessageBase>> BuildMessagesUsingStandardsAsync(
        this List<ContentBlock> blocks,
        LLMProviders selectedProvider,
        Model selectedModel) => await blocks.BuildMessagesAsync(
            selectedProvider,
            selectedModel,
            StandardRoleTransformer,
            StandardTextSubContentFactory,
            StandardImageSubContentFactory);

    private static ISubContent StandardTextSubContentFactory(string text) => new SubContentText
    {
        Text = text,
    };
    
    private static async Task<ISubContent> StandardImageSubContentFactory(FileAttachmentImage attachment) => new SubContentImageUrl
    {
        ImageUrl = await attachment.TryAsBase64() is (true, var base64Content)
            ? $"data:{attachment.DetermineMimeType()};base64,{base64Content}"
            : string.Empty,
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