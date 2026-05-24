using System.Collections.Concurrent;

using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.RAG.RAGProcesses;

namespace AIStudio.Tools.AIJobs;

public sealed class AIJobService(
    SettingsManager settingsManager,
    MessageBus messageBus,
    ILogger<AIJobService> logger)
{
    private sealed class AIJobState
    {
        public required CancellationTokenSource CancellationTokenSource { get; init; }

        public required ChatGenerationRequest ChatGenerationRequest { get; init; }

        public required AIJobSnapshot Snapshot { get; set; }

        public DateTimeOffset LastCheckpoint { get; set; }

        public readonly Lock SyncRoot = new();
    }

    private static readonly TimeSpan STREAMING_EVENT_MIN_TIME = TimeSpan.FromSeconds(3);
    
    private static readonly TimeSpan CHECKPOINT_MIN_TIME = TimeSpan.FromSeconds(3);
    
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(AIJobService).Namespace, nameof(AIJobService));

    private readonly ConcurrentDictionary<Guid, AIJobState> jobs = new();
    private readonly ConcurrentDictionary<Guid, Guid> activeChatJobsByChatId = new();

    public IReadOnlyCollection<AIJobSnapshot> GetSnapshots()
    {
        return this.jobs.Values
            .Select(job => job.Snapshot)
            .OrderByDescending(snapshot => snapshot.UpdatedAt)
            .ToList();
    }

    public bool HasActiveJobs => this.jobs.Values.Any(job => job.Snapshot.IsActive);

    public bool IsChatGenerationActive(Guid chatId)
    {
        if (!this.activeChatJobsByChatId.TryGetValue(chatId, out var jobId))
            return false;

        return this.jobs.TryGetValue(jobId, out var job) && job.Snapshot.IsActive;
    }

    public AIJobSnapshot? TryGetChatSnapshot(Guid chatId)
    {
        if (!this.activeChatJobsByChatId.TryGetValue(chatId, out var jobId))
            return this.jobs.Values
                .Select(job => job.Snapshot)
                .Where(snapshot => snapshot.Kind is AIJobKind.CHAT_GENERATION && snapshot.SubjectId == chatId)
                .MaxBy(snapshot => snapshot.UpdatedAt);

        return this.jobs.TryGetValue(jobId, out var activeJob) ? activeJob.Snapshot : null;
    }

    public ChatThread? TryGetLiveChatThread(Guid chatId)
    {
        if (!this.activeChatJobsByChatId.TryGetValue(chatId, out var jobId))
            return null;

        return this.jobs.TryGetValue(jobId, out var job) ? job.ChatGenerationRequest.ChatThread : null;
    }

    public async Task<AIJobSnapshot?> TryStartChatGenerationAsync(ChatGenerationRequest request)
    {
        if (this.activeChatJobsByChatId.TryGetValue(request.ChatThread.ChatId, out var existingJobId))
            return this.jobs.TryGetValue(existingJobId, out var existingJob) ? existingJob.Snapshot : null;

        var jobId = Guid.NewGuid();
        var rootJobId = request.ParentJobId ?? jobId;
        var snapshot = new AIJobSnapshot
        {
            JobId = jobId,
            Kind = AIJobKind.CHAT_GENERATION,
            SubjectId = request.ChatThread.ChatId,
            ParentJobId = request.ParentJobId,
            RootJobId = rootJobId,
            Priority = request.Priority,
            IsForeground = request.IsForeground,
            SchedulingClass = AIJobSchedulingClass.TOP_LEVEL_USER_JOB,
            Status = AIJobStatus.WAITING_FOR_REMOTE,
            Title = request.ChatThread.Name,
            ProviderId = request.ProviderSettings.Id,
            ModelId = request.ProviderSettings.Model.Id,
            UpdatedAt = DateTimeOffset.Now,
        };

        var state = new AIJobState
        {
            CancellationTokenSource = new CancellationTokenSource(),
            ChatGenerationRequest = request,
            Snapshot = snapshot,
            LastCheckpoint = DateTimeOffset.MinValue,
        };

        if (!this.activeChatJobsByChatId.TryAdd(request.ChatThread.ChatId, jobId))
        {
            state.CancellationTokenSource.Dispose();
            return this.TryGetChatSnapshot(request.ChatThread.ChatId);
        }

        if (!this.jobs.TryAdd(jobId, state))
        {
            this.activeChatJobsByChatId.TryRemove(request.ChatThread.ChatId, out _);
            state.CancellationTokenSource.Dispose();
            return null;
        }

        request.AIText.InitialRemoteWait = true;
        request.AIText.IsStreaming = false;
        await CheckpointChatAsync(state, force: true);
        await this.NotifyChangedAsync(state);

        _ = Task.Factory.StartNew(async () => await this.RunChatGenerationAsync(state), TaskCreationOptions.LongRunning);
        return state.Snapshot;
    }

    public async Task CancelAsync(Guid jobId)
    {
        if (!this.jobs.TryGetValue(jobId, out var job))
            return;

        if (!job.CancellationTokenSource.IsCancellationRequested)
            await job.CancellationTokenSource.CancelAsync();
    }

    public async Task CancelChatGenerationAsync(Guid chatId)
    {
        if (!this.activeChatJobsByChatId.TryGetValue(chatId, out var jobId))
            return;

        await this.CancelAsync(jobId);
    }

    public async Task SetForegroundAsync(AIJobKind kind, Guid subjectId, bool isForeground)
    {
        var matchingJobs = this.jobs.Values
            .Where(job => job.Snapshot.Kind == kind && job.Snapshot.SubjectId == subjectId && job.Snapshot.IsActive)
            .ToList();

        foreach (var job in matchingJobs)
        {
            lock (job.SyncRoot)
            {
                job.Snapshot = job.Snapshot with
                {
                    IsForeground = isForeground,
                    UpdatedAt = DateTimeOffset.Now,
                };
            }

            await this.NotifyChangedAsync(job);
        }
    }

    private async Task RunChatGenerationAsync(AIJobState state)
    {
        var request = state.ChatGenerationRequest;
        var token = state.CancellationTokenSource.Token;

        try
        {
            var provider = request.ProviderSettings.CreateProvider();
            var chatThread = request.ChatThread;
            var aiText = request.AIText;

            if (!chatThread.IsLLMProviderAllowed(provider))
            {
                logger.LogError("The provider is not allowed for chat '{ChatId}' due to data security reasons. Skipping the AI process.", chatThread.ChatId);
                await this.CompleteChatGenerationAsync(state, AIJobStatus.FAILED, TB("The selected provider is not allowed for this chat."));
                return;
            }

            if (!await this.CheckSelectedModelAvailability(provider, request.ProviderSettings.Model, token))
            {
                await this.CompleteChatGenerationAsync(state, AIJobStatus.FAILED, TB("The selected model is not available."));
                return;
            }

            try
            {
                var rag = new AISrcSelWithRetCtxVal();
                if (request.LastUserPrompt is not null)
                {
                    chatThread = await rag.ProcessAsync(provider, request.LastUserPrompt, chatThread, token);
                    request.ChatThread = chatThread;
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                await this.CompleteChatGenerationAsync(state, AIJobStatus.CANCELED);
                return;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Skipping the RAG process due to an error.");
            }

            var lastStreamingEvent = DateTimeOffset.MinValue;
            aiText.InitialRemoteWait = true;
            
            await this.NotifyChangedAsync(state);
            await foreach (var contentStreamChunk in provider.StreamChatCompletion(request.ProviderSettings.Model, chatThread, settingsManager, token))
            {
                if (token.IsCancellationRequested)
                    break;

                aiText.InitialRemoteWait = false;
                aiText.IsStreaming = true;
                aiText.Text += contentStreamChunk;
                aiText.Sources.MergeSources(contentStreamChunk.Sources);

                UpdateStatus(state, AIJobStatus.RUNNING);
                var now = DateTimeOffset.Now;
                if (!settingsManager.ConfigurationData.App.IsSavingEnergy || now - lastStreamingEvent > STREAMING_EVENT_MIN_TIME)
                {
                    lastStreamingEvent = now;
                    await this.NotifyChangedAsync(state);
                }

                await CheckpointChatAsync(state);
            }

            await this.CompleteChatGenerationAsync(state, token.IsCancellationRequested ? AIJobStatus.CANCELED : AIJobStatus.COMPLETED);
        }
        catch (OperationCanceledException)
        {
            await this.CompleteChatGenerationAsync(state, AIJobStatus.CANCELED);
        }
        catch (Exception e)
        {
            logger.LogError(e, "The chat generation job '{JobId}' failed.", state.Snapshot.JobId);
            await this.CompleteChatGenerationAsync(state, AIJobStatus.FAILED, e.Message);
            await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Stream, string.Format(TB("The AI job failed. The message is: '{0}'"), e.Message)));
        }
    }

    private async Task CompleteChatGenerationAsync(AIJobState state, AIJobStatus status, string errorMessage = "")
    {
        var aiText = state.ChatGenerationRequest.AIText;
        aiText.InitialRemoteWait = false;
        aiText.IsStreaming = false;
        aiText.Text = aiText.Text.RemoveThinkTags().Trim();

        lock (state.SyncRoot)
        {
            state.Snapshot = state.Snapshot with
            {
                Status = status,
                ErrorMessage = errorMessage,
                UpdatedAt = DateTimeOffset.Now,
            };
        }

        this.activeChatJobsByChatId.TryRemove(state.ChatGenerationRequest.ChatThread.ChatId, out _);
        await CheckpointChatAsync(state, force: true);
        await this.NotifyChangedAsync(state);
        await messageBus.SendMessage(null, Event.AI_JOB_FINISHED, state.Snapshot);
        state.CancellationTokenSource.Dispose();
    }

    private static void UpdateStatus(AIJobState state, AIJobStatus status)
    {
        lock (state.SyncRoot)
        {
            if (state.Snapshot.Status == status)
                return;

            state.Snapshot = state.Snapshot with
            {
                Status = status,
                UpdatedAt = DateTimeOffset.Now,
            };
        }
    }

    private async Task NotifyChangedAsync(AIJobState state)
    {
        lock (state.SyncRoot)
        {
            state.Snapshot = state.Snapshot with
            {
                Title = state.ChatGenerationRequest.ChatThread.Name,
                UpdatedAt = DateTimeOffset.Now,
            };
        }

        await messageBus.SendMessage(null, Event.AI_JOB_CHANGED, state.Snapshot);
    }

    private static async Task CheckpointChatAsync(AIJobState state, bool force = false)
    {
        var now = DateTimeOffset.Now;
        if (!force && now - state.LastCheckpoint < CHECKPOINT_MIN_TIME)
            return;

        state.LastCheckpoint = now;
        await WorkspaceBehaviour.StoreChatAsync(state.ChatGenerationRequest.ChatThread);
    }

    private static bool ModelsMatch(Model modelA, Model modelB)
    {
        var idA = modelA.Id.Trim();
        var idB = modelB.Id.Trim();
        return string.Equals(idA, idB, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> CheckSelectedModelAvailability(IProvider provider, Model chatModel, CancellationToken token = default)
    {
        if (chatModel.IsSystemModel)
            return true;

        if (string.IsNullOrWhiteSpace(chatModel.Id))
        {
            logger.LogWarning("Skipping AI request because model ID is null or white space.");
            return false;
        }

        if (!provider.HasModelLoadingCapability)
            return true;

        IReadOnlyList<Model> loadedModels;
        try
        {
            var modelLoadResult = await provider.GetTextModels(token: token);
            if (!modelLoadResult.Success)
            {
                var userMessage = modelLoadResult.FailureReason.ToUserMessage(provider.InstanceName);
                if (!string.IsNullOrWhiteSpace(userMessage))
                    await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.CloudOff, userMessage));

                logger.LogWarning("Skipping selected model availability check for '{ProviderInstanceName}' (provider={ProviderType}) because loading the model list failed with reason {FailureReason}.", provider.InstanceName, provider.Provider, modelLoadResult.FailureReason);
                return false;
            }

            loadedModels = modelLoadResult.Models;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Skipping selected model availability check for '{ProviderInstanceName}' (provider={ProviderType}) because the model list could not be loaded.", provider.InstanceName, provider.Provider);
            return true;
        }

        var availableModels = loadedModels.Where(model => !string.IsNullOrWhiteSpace(model.Id)).ToList();
        if (availableModels.Count == 0)
        {
            var emptyModelsMessage = string.Format(
                TB("We could load models from '{0}', but the provider did not return any usable text models."),
                provider.InstanceName);

            await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.CloudOff, emptyModelsMessage));
            logger.LogWarning("Skipping AI request because there are no models available from '{ProviderInstanceName}' (provider={ProviderType}).", provider.InstanceName, provider.Provider);
            return false;
        }

        if (availableModels.Any(model => ModelsMatch(model, chatModel)))
            return true;

        var message = string.Format(
            TB("The selected model '{0}' is no longer available from '{1}' (provider={2}). Please adapt your provider settings."),
            chatModel.Id,
            provider.InstanceName,
            provider.Provider);

        await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.CloudOff, message));
        logger.LogWarning("Skipping AI request because model '{ModelId}' is not available from '{ProviderInstanceName}' (provider={ProviderType}).", chatModel.Id, provider.InstanceName, provider.Provider);
        return false;
    }
}