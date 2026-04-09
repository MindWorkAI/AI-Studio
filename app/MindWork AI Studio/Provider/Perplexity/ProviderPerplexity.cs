using System.Runtime.CompilerServices;

using AIStudio.Chat;
using AIStudio.Provider.OpenAI;
using AIStudio.Settings;

namespace AIStudio.Provider.Perplexity;

public sealed class ProviderPerplexity() : BaseProvider(LLMProviders.PERPLEXITY, "https://api.perplexity.ai/", LOGGER)
{
    private static readonly ILogger<ProviderPerplexity> LOGGER = Program.LOGGER_FACTORY.CreateLogger<ProviderPerplexity>();

    private static readonly Model[] KNOWN_MODELS =
    [
        new("sonar", "Sonar"),
        new("sonar-pro", "Sonar Pro"),
        new("sonar-reasoning", "Sonar Reasoning"),
        new("sonar-reasoning-pro", "Sonar Reasoning Pro"),
        new("sonar-deep-research", "Sonar Deep Research"),
    ];
    
    #region Implementation of IProvider

    /// <inheritdoc />
    public override string Id => LLMProviders.PERPLEXITY.ToName();

    /// <inheritdoc />
    public override string InstanceName { get; set; } = "Perplexity";
    
    /// <inheritdoc />
    public override async IAsyncEnumerable<ContentStreamChunk> StreamChatCompletion(Model chatModel, ChatThread chatThread, SettingsManager settingsManager, [EnumeratorCancellation] CancellationToken token = default)
    {
        await foreach (var content in this.StreamOpenAICompatibleChatCompletion<ResponseStreamLine, NoChatCompletionAnnotationStreamLine>(
                           "Perplexity",
                           chatModel,
                           chatThread,
                           settingsManager,
                           () => chatThread.Blocks.BuildMessagesUsingNestedImageUrlAsync(this.Provider, chatModel),
                           (systemPrompt, messages, apiParameters, stream, tools) =>
                               Task.FromResult(new ChatCompletionAPIRequest
                               {
                                   Model = chatModel.Id,
                                   Messages = [systemPrompt, ..messages],
                                   Stream = stream,
                                   Tools = tools,
                                   ParallelToolCalls = tools is null ? null : true,
                                   AdditionalApiParameters = apiParameters
                               }),
                           token: token))
            yield return content;
    }

    #pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    /// <inheritdoc />
    public override async IAsyncEnumerable<ImageURL> StreamImageCompletion(Model imageModel, string promptPositive, string promptNegative = FilterOperator.String.Empty, ImageURL referenceImageURL = default, [EnumeratorCancellation] CancellationToken token = default)
    {
        yield break;
    }
    #pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    
    /// <inheritdoc />
    public override Task<string> TranscribeAudioAsync(Model transcriptionModel, string audioFilePath, SettingsManager settingsManager, CancellationToken token = default)
    {
        return Task.FromResult(string.Empty);
    }
    
    /// <inhertidoc />
    public override Task<IReadOnlyList<IReadOnlyList<float>>> EmbedTextAsync(Model embeddingModel, SettingsManager settingsManager, CancellationToken token = default, params List<string> texts)
    {
        return Task.FromResult<IReadOnlyList<IReadOnlyList<float>>>([]);
    }

    /// <inheritdoc />
    public override Task<IEnumerable<Model>> GetTextModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return this.LoadModels();
    }

    /// <inheritdoc />
    public override Task<IEnumerable<Model>> GetImageModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(Enumerable.Empty<Model>());
    }
    
    /// <inheritdoc />
    public override Task<IEnumerable<Model>> GetEmbeddingModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(Enumerable.Empty<Model>());
    }
    
    /// <inheritdoc />
    public override Task<IEnumerable<Model>> GetTranscriptionModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(Enumerable.Empty<Model>());
    }
    
    #endregion

    private Task<IEnumerable<Model>> LoadModels() => Task.FromResult<IEnumerable<Model>>(KNOWN_MODELS);
}
