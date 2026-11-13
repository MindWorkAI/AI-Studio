using System.Collections.Concurrent;

using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.AssistantSessions;

namespace AIStudio.Chat.Embedded;

/// <summary>
/// Runs the chats which assistants show on their own pages, such as the two chats of a task of the Model Comparison
/// assistant.
/// </summary>
/// <remarks>
/// A singleton, so that an answer goes on while the user looks at another page: the page asks for the chat again when
/// it comes back and listens to ConversationChanged. Each request registers in AssistantSessionService, which shows the
/// work in the navigation. An embedded chat lives in a store of its assistant, never in a workspace, see
/// IEmbeddedChatStore. What a request carries beside the conversation, such as the system prompt or the tools, the
/// assistant decides for every request, see EmbeddedChatRequest.
/// </remarks>
/// <param name="settingsManager">The settings.</param>
/// <param name="sessionService">The registry of running assistant sessions.</param>
/// <param name="messageBus">The message bus, for errors.</param>
/// <param name="logger">The logger.</param>
/// <param name="answerAsync">Lets the model answer a request; asks the provider by default, and is replaced in tests.</param>
public sealed class EmbeddedChatService(SettingsManager settingsManager, AssistantSessionService sessionService, MessageBus messageBus, ILogger<EmbeddedChatService> logger, Func<EmbeddedChatRequest, ContentText, ChatThread, CancellationToken, Task>? answerAsync = null)
{
    /// <summary>
    /// How often a streamed answer renders at most.
    /// </summary>
    private static readonly TimeSpan STREAMING_RENDER_MIN_TIME = TimeSpan.FromMilliseconds(100);

    private static string TA(string fallbackEN) => Tools.PluginSystem.I18N.I.TA(fallbackEN, typeof(EmbeddedChatService).Namespace, nameof(EmbeddedChatService));

    /// <summary>
    /// The chats loaded so far, by their IDs.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, EmbeddedConversation> conversations = new();

    /// <summary>
    /// Serializes loading a chat, so that two pages never load the same chat twice.
    /// </summary>
    private readonly SemaphoreSlim loadLock = new(1, 1);

    /// <summary>
    /// Guards whether a chat answers right now, so that two requests never run in the same chat.
    /// </summary>
    private readonly Lock runningLock = new();

    private readonly Func<EmbeddedChatRequest, ContentText, ChatThread, CancellationToken, Task> answerAsync = answerAsync ?? AnswerWithProviderAsync;

    /// <summary>
    /// Happens whenever a chat changed: a message, a chunk of an answer, or the end of a request. It names the ID of
    /// the chat.
    /// </summary>
    /// <remarks>
    /// Called on the thread of the request. A page renders through InvokeAsync.
    /// </remarks>
    public event Func<Guid, Task>? ConversationChanged;

    /// <summary>
    /// Gets a chat, loading it on the first call.
    /// </summary>
    /// <param name="store">Where the chat lives.</param>
    /// <param name="component">The assistant which shows the chat.</param>
    /// <param name="chatId">The ID of the chat.</param>
    /// <param name="createThread">Creates the thread of a chat which has none stored yet; it gets the ID of the chat.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The chat; it is not available when this version cannot open the stored one.</returns>
    public async Task<EmbeddedConversation> GetAsync(IEmbeddedChatStore store, Tools.Components component, Guid chatId, Func<ChatThread> createThread, CancellationToken token = default)
    {
        if (this.conversations.TryGetValue(chatId, out var conversation))
            return conversation;

        await this.loadLock.WaitAsync(token);
        try
        {
            if (this.conversations.TryGetValue(chatId, out conversation))
                return conversation;

            ChatThread? thread;
            try
            {
                thread = await store.LoadThreadAsync(chatId, token);
                if (thread is null)
                {
                    thread = createThread();
                    thread.ChatId = chatId;
                }
            }
            catch (InvalidDataException exception)
            {
                // The file stays as it is, see IEmbeddedChatStore.LoadThreadAsync:
                logger.LogWarning(exception, "Could not open the embedded chat '{ChatId}' of the component '{Component}'.", chatId, component);
                thread = null;
            }

            conversation = new EmbeddedConversation { ChatId = chatId, Component = component, Store = store, Thread = thread };
            this.conversations[chatId] = conversation;
            return conversation;
        }
        finally
        {
            this.loadLock.Release();
        }
    }

    /// <summary>
    /// Sends a message and lets the model answer it.
    /// </summary>
    /// <remarks>
    /// The message is stored before the model answers, so that a crash loses at most the answer. What goes wrong is
    /// reported through the message bus; the method itself does not fail.
    /// </remarks>
    /// <param name="chatId">The ID of a chat which GetAsync loaded before.</param>
    /// <param name="request">The message and what the request carries beside it.</param>
    /// <returns>True when the message went into the chat, even when the model could not answer it.</returns>
    public async Task<bool> SendAsync(Guid chatId, EmbeddedChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message.Text) || !this.conversations.TryGetValue(chatId, out var conversation) || conversation.Thread is not { } thread)
            return false;

        lock (this.runningLock)
        {
            if (conversation.IsRunning)
                return false;

            conversation.IsRunning = true;
        }

        var messageIsStored = false;
        ContentBlock? answerBlock = null;
        var sessionKey = SessionKey(conversation);
        var sessionId = Guid.Empty;
        var status = AssistantSessionStatus.FAILED;
        try
        {
            if (!await request.PrepareAsync(thread) || !await this.CheckProviderAsync(conversation, thread, request.Provider))
                return false;

            thread.RuntimeComponent = conversation.Component;
            thread.Blocks.Add(new ContentBlock
            {
                Time = DateTimeOffset.Now,
                ContentType = ContentType.TEXT,
                Role = ChatRole.USER,
                Content = request.Message,
                HideFromUser = request.IsHidden,
            });

            await conversation.Store.SaveThreadAsync(thread);
            messageIsStored = true;

            var answer = new ContentText { InitialRemoteWait = true };
            answerBlock = new ContentBlock
            {
                Time = DateTimeOffset.Now,
                ContentType = ContentType.TEXT,
                Role = ChatRole.AI,
                Content = answer,
            };

            var lastRendering = DateTimeOffset.MinValue;
            answer.StreamingEvent = async () =>
            {
                var now = DateTimeOffset.Now;
                if (now - lastRendering < STREAMING_RENDER_MIN_TIME)
                    return;

                lastRendering = now;
                await this.NotifyAsync(chatId);
            };

            answer.StreamingDone = () => this.NotifyAsync(chatId);
            thread.Blocks.Add(answerBlock);

            var cancellation = new CancellationTokenSource();
            sessionId = (await sessionService.TryBeginAsync(sessionKey, conversation.Component.Name, cancellation, thread, new(StringComparer.Ordinal))).SessionId;
            await this.NotifyAsync(chatId);

            await this.answerAsync(request, answer, request.ThreadForRequest(thread), cancellation.Token);
            status = cancellation.IsCancellationRequested ? AssistantSessionStatus.CANCELED : AssistantSessionStatus.COMPLETED;
        }
        catch (OperationCanceledException)
        {
            status = AssistantSessionStatus.CANCELED;
        }
        catch (ProviderRequestException exception)
        {
            logger.LogError(exception, "The provider request of an embedded chat failed. Status={StatusCode}, Reason='{ReasonPhrase}'.", exception.StatusCode, exception.ReasonPhrase);
            await messageBus.SendError(new(AppIcons.PROVIDER_ERROR, exception.UserMessage));
        }
        catch (FileNotFoundException)
        {
            // What the chat belonged to was deleted before the message went in; nothing is left to store.
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            logger.LogError(exception, "Could not store the embedded chat '{ChatId}'.", chatId);
            await messageBus.SendError(new(AppIcons.SAVE_FAILED, TA("AI Studio could not save this conversation. Please try again.")));
        }
        finally
        {
            await this.FinishAsync(conversation, thread, answerBlock, sessionKey, sessionId, status);
        }

        return messageIsStored;
    }

    /// <summary>
    /// Stops the answer the model writes in a chat.
    /// </summary>
    /// <param name="chatId">The ID of the chat.</param>
    public Task CancelAsync(Guid chatId) => this.conversations.TryGetValue(chatId, out var conversation)
        ? sessionService.CancelAsync(SessionKey(conversation))
        : Task.CompletedTask;

    /// <summary>
    /// Stops the answer of a chat and forgets the chat, e.g., because the user deleted what it belonged to.
    /// </summary>
    /// <param name="chatId">The ID of the chat.</param>
    public async Task ForgetAsync(Guid chatId)
    {
        await this.CancelAsync(chatId);
        this.conversations.TryRemove(chatId, out _);
    }

    /// <summary>
    /// Lets the provider of a request answer it.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="answer">The answer, which the stream fills.</param>
    /// <param name="thread">The chat which the request sends.</param>
    /// <param name="token">The cancellation token.</param>
    private static async Task AnswerWithProviderAsync(EmbeddedChatRequest request, ContentText answer, ChatThread thread, CancellationToken token)
    {
        var provider = request.Provider.CreateProvider();
        provider.HidesIdentity = request.HidesModelIdentity;
        await answer.CreateFromProviderAsync(provider, request.Provider.Model, request.Message, thread, token);
    }

    /// <summary>
    /// Checks whether a provider may answer in a chat, and tells the user why not.
    /// </summary>
    /// <param name="conversation">The chat.</param>
    /// <param name="thread">The thread of the chat, readied for the request.</param>
    /// <param name="provider">The provider.</param>
    /// <returns>True when the provider may answer.</returns>
    private async Task<bool> CheckProviderAsync(EmbeddedConversation conversation, ChatThread thread, AIStudio.Settings.Provider provider)
    {
        string? reason = null;
        if (provider.UsedLLMProvider is LLMProviders.NONE)
            reason = TA("Please select a provider.");
        else if (!settingsManager.IsProviderConfident(provider, conversation.Component) || !thread.IsLLMProviderAllowed(provider))
            reason = TA("The selected provider may not see this conversation. Please select another provider.");

        if (reason is null)
            return true;

        await messageBus.SendError(new(AppIcons.PROVIDER_ERROR, reason));
        return false;
    }

    /// <summary>
    /// Ends a request: removes an empty answer, stores the chat, and closes the session.
    /// </summary>
    /// <param name="conversation">The chat.</param>
    /// <param name="thread">The thread of the chat.</param>
    /// <param name="answerBlock">The block of the answer, or null when the request did not get that far.</param>
    /// <param name="sessionKey">The key of the session.</param>
    /// <param name="sessionId">The ID of the session, or Guid.Empty when none began.</param>
    /// <param name="status">How the request ended.</param>
    private async Task FinishAsync(EmbeddedConversation conversation, ChatThread thread, ContentBlock? answerBlock, AssistantSessionKey sessionKey, Guid sessionId, AssistantSessionStatus status)
    {
        if (answerBlock is { Content: ContentText answer } && string.IsNullOrWhiteSpace(answer.Text))
            thread.Blocks.Remove(answerBlock);

        if (answerBlock is not null)
        {
            try
            {
                await conversation.Store.SaveThreadAsync(thread);
            }
            catch (FileNotFoundException)
            {
                // What the chat belonged to was deleted while the model answered; nothing is left to store.
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                logger.LogError(exception, "Could not store the embedded chat '{ChatId}'.", conversation.ChatId);
                await messageBus.SendError(new(AppIcons.SAVE_FAILED, TA("AI Studio could not save this conversation. Please try again.")));
            }
        }

        lock (this.runningLock)
            conversation.IsRunning = false;

        if (sessionId != Guid.Empty)
        {
            await sessionService.CompleteAsync(sessionKey, sessionId, status, string.Empty, thread, new(StringComparer.Ordinal));

            // The assistant keeps the chat itself; the registry only shows that the assistant is at work:
            await sessionService.ClearAsync(sessionKey);
        }

        await this.NotifyAsync(conversation.ChatId);
    }

    /// <summary>
    /// Tells every page which listens that a chat changed.
    /// </summary>
    /// <param name="chatId">The ID of the chat.</param>
    private async Task NotifyAsync(Guid chatId)
    {
        if (this.ConversationChanged is not { } handlers)
            return;

        foreach (var handler in handlers.GetInvocationList().Cast<Func<Guid, Task>>())
        {
            try
            {
                await handler(chatId);
            }
            catch (Exception exception)
            {
                // A page which went away meanwhile must not stop the answer:
                logger.LogDebug(exception, "A page could not show a change of an embedded chat.");
            }
        }
    }

    /// <summary>
    /// Gets the key under which a request of a chat registers in AssistantSessionService.
    /// </summary>
    /// <param name="conversation">The chat.</param>
    private static AssistantSessionKey SessionKey(EmbeddedConversation conversation) => new(conversation.Component, conversation.ChatId.ToString("D"));
}