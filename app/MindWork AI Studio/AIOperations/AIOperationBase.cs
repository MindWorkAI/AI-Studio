using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;

namespace AIStudio.AIOperations;

/// <summary>
/// The base of the AI operations, with what each of them needs to ask a model.
/// </summary>
/// <remarks>
/// It keeps nothing of a call: every request gets a chat thread of its own, and the provider comes with the
/// context. Derived operations keep it that way, see IAIOperation; AIOperationsAreStatelessTests checks it.
/// </remarks>
/// <typeparam name="TRequest">What the operation works on.</typeparam>
/// <typeparam name="TResult">What the operation returns.</typeparam>
public abstract class AIOperationBase<TRequest, TResult>(ILogger logger, SettingsManager settingsManager) : IAIOperation<TRequest, TResult>
{
    protected ILogger Logger { get; } = logger;

    protected SettingsManager SettingsManager { get; } = settingsManager;

    #region Implementation of IAIOperation

    /// <inheritdoc />
    public abstract string Id { get; }

    /// <inheritdoc />
    public abstract Tools.Components Component { get; }

    /// <inheritdoc />
    public abstract ConfidenceLevel GetRequiredConfidence(TRequest request);

    /// <inheritdoc />
    public abstract Task<TResult> RunAsync(TRequest request, AIOperationContext context, CancellationToken token = default);

    #endregion

    /// <summary>
    /// Sends one request to the provider of the context and waits for the whole answer.
    /// </summary>
    /// <remarks>
    /// The provider is checked once more right before: the organization must not have prohibited it, and it
    /// has to reach the global minimum and the confidence the content of the request requires. The minimum
    /// of the operation itself is not checked here, because a caller may exempt a fallback from it.
    /// </remarks>
    /// <param name="request">The request, for the confidence its content requires.</param>
    /// <param name="context">What the call runs with, the provider above all.</param>
    /// <param name="systemPrompt">The system prompt.</param>
    /// <param name="userPrompt">The user prompt.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The text of the answer, or null when the provider must not see the request or did not answer.</returns>
    protected async Task<string?> AskAsync(TRequest request, AIOperationContext context, string systemPrompt, string userPrompt, CancellationToken token)
    {
        var provider = context.Provider;
        if (!this.MayAsk(provider, request))
            return null;

        var thread = new ChatThread
        {
            WorkspaceId = Guid.Empty,
            ChatId = Guid.NewGuid(),
            Name = string.Empty,
            SystemPrompt = systemPrompt,
            Blocks = [],
            RuntimeComponent = this.Component,
        };

        var time = DateTimeOffset.Now;
        var prompt = new ContentText
        {
            Text = userPrompt,
        };

        thread.Blocks.Add(new ContentBlock
        {
            Time = time,
            ContentType = ContentType.TEXT,
            Role = ChatRole.USER,
            Content = prompt,
        });

        var answer = new ContentText
        {
            // We have to wait for the remote for the content stream:
            InitialRemoteWait = true,
        };

        thread.Blocks.Add(new ContentBlock
        {
            Time = time,
            ContentType = ContentType.TEXT,
            Role = ChatRole.AI,
            Content = answer,
        });

        // Awaiting this line waits for the entire content to be streamed:
        await answer.CreateFromProviderAsync(provider.CreateProvider(), provider.Model, prompt, thread, token);
        if (string.IsNullOrWhiteSpace(answer.Text))
        {
            this.Logger.LogWarning($"The AI operation '{this.Id}' got no answer from the provider '{provider.InstanceName}'.");
            return null;
        }

        return answer.Text;
    }

    private bool MayAsk(Settings.Provider provider, TRequest request)
    {
        if (provider == Settings.Provider.NONE || provider.UsedLLMProvider is LLMProviders.NONE)
        {
            this.Logger.LogWarning($"The AI operation '{this.Id}' has no provider.");
            return false;
        }

        var minimumLevel = this.SettingsManager.GlobalMinimumConfidenceLevel;
        var requiredLevel = this.GetRequiredConfidence(request);
        if (requiredLevel > minimumLevel)
            minimumLevel = requiredLevel;

        if (!this.SettingsManager.IsProviderUsable(provider.UsedLLMProvider, minimumLevel))
        {
            this.Logger.LogWarning($"The AI operation '{this.Id}' must not use the provider '{provider.InstanceName}': the organization prohibited it, or its confidence is below '{minimumLevel.Name}'.");
            return false;
        }

        return true;
    }
}