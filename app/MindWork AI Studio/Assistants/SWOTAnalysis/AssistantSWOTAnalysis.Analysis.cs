using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;

namespace AIStudio.Assistants.SWOTAnalysis;

public partial class AssistantSWOTAnalysis
{
    private async Task AnalyzeText()
    {
        await this.Form!.Validate();
        if (!this.InputIsValid)
            return;

        this.ClearInputIssues();
        this.ClearConversationState();
        this.analysisResult = null;
        this.sharedResearchContext = string.Empty;
        this.sharedResearchAnswer = null;
        this.isGatheringSharedResearch = false;
        this.isFinalizingAnalysis = false;
        this.categoryAnalysisProgress.Clear();
        this.CreateChatThread();

        var token = this.CancellationTokenSource?.Token ?? CancellationToken.None;
        // The research runs in a thread of its own, which has read nothing yet:
        var runnableToolIds = this.GetRunnableToolIds(OutboundDataRestriction.UNRESTRICTED);
        if (runnableToolIds.Count > 0)
        {
            this.isGatheringSharedResearch = true;
            await this.PublishAnalysisProgressAsync();
            try
            {
                this.sharedResearchContext = await this.GatherSharedResearchAsync(runnableToolIds, token);
            }
            finally
            {
                this.isGatheringSharedResearch = false;
                await this.PublishAnalysisProgressAsync();
            }

            if (token.IsCancellationRequested)
                return;
        }

        var categories = Enum.GetValues<SwotAnalysisCategory>();
        this.categoryAnalysisProgress = categories.ToDictionary(
            category => category,
            _ => new SwotCategoryAnalysisProgress(SwotCategoryAnalysisStatus.RUNNING));
        await this.PublishAnalysisProgressAsync();

        var categoryTasks = categories.ToDictionary(
            category => this.AnalyzeCategoryAsync(category, token),
            category => category);

        while (categoryTasks.Count > 0)
        {
            var completedTask = await Task.WhenAny(categoryTasks.Keys);
            var category = categoryTasks[completedTask];
            categoryTasks.Remove(completedTask);
            this.categoryAnalysisProgress[category] = await completedTask;
            await this.PublishAnalysisProgressAsync();
        }

        if (token.IsCancellationRequested)
            return;

        if (this.categoryAnalysisProgress.Values.Any(progress => progress.Status is not SwotCategoryAnalysisStatus.COMPLETED))
        {
            this.AddInputIssue(T("At least one SWOT category could not be created. The completed categories remain visible below."));
            return;
        }

        var categoryResults = this.categoryAnalysisProgress.ToDictionary(
            item => item.Key,
            item => item.Value.Result!);

        await this.FinalizeAnalysisAsync(categoryResults, token);
    }

    private async Task<SwotCategoryAnalysisProgress> AnalyzeCategoryAsync(SwotAnalysisCategory category, CancellationToken token)
    {
        var userPrompt = new ContentText
        {
            Text = SwotCategoryAnalysis.BuildUserRequest(
                this.inputText,
                this.analysisGoal,
                this.importantAspects,
                this.contextMaterials.Count > 0,
                this.sharedResearchContext),
            FileAttachments = [.. this.contextMaterials],
        };
        var thread = new ChatThread
        {
            IncludeDateTime = false,
            SelectedProvider = this.ProviderSettings.Id,
            SelectedProfile = Profile.NO_PROFILE.Id,
            SelectedToolIds = [.. this.SelectedToolIds],
            SystemPrompt = SwotCategoryAnalysis.BuildSystemPrompt(category, this.OutputLanguageInstruction),
            WorkspaceId = Guid.Empty,
            ChatId = Guid.NewGuid(),
            Name = $"{this.Title} - {category}",
            Blocks = [],
            RuntimeSelectedToolIds = [],
            RuntimeToolsAreAssistantManaged = this.AssistantManagedToolIds is not null,
        };
        this.AssignRuntimeIdentity(thread);
        thread.Blocks.Add(CreateBlock(DateTimeOffset.Now, ChatRole.USER, userPrompt));

        var aiText = new ContentText { InitialRemoteWait = true };
        thread.Blocks.Add(CreateBlock(DateTimeOffset.Now, ChatRole.AI, aiText));

        try
        {
            await aiText.CreateFromProviderAsync(
                this.ProviderSettings.CreateProvider(),
                this.ProviderSettings.Model,
                userPrompt,
                thread,
                token);

            if (token.IsCancellationRequested)
                return new(SwotCategoryAnalysisStatus.CANCELED, Message: T("The category analysis was canceled."));

            if (!SwotCategoryAnalysisResponseParser.TryParse(aiText.Text, out var result, out var issue))
            {
                this.Logger.LogWarning("The answer for the SWOT category '{SwotCategory}' could not be read: {Issue}", category, issue);
                return new(SwotCategoryAnalysisStatus.FAILED, Message: T("The model response for this category did not have the expected structure."));
            }

            return new(SwotCategoryAnalysisStatus.COMPLETED, result);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return new(SwotCategoryAnalysisStatus.CANCELED, Message: T("The category analysis was canceled."));
        }
        catch (ProviderRequestException exception)
        {
            this.Logger.LogError(
                exception,
                "The provider request failed for SWOT category '{SwotCategory}'. Status={StatusCode}, Reason='{ReasonPhrase}', Body='{ResponseBody}'",
                category,
                exception.StatusCode,
                exception.ReasonPhrase,
                exception.ResponseBody);
            return new(SwotCategoryAnalysisStatus.FAILED, Message: exception.UserMessage);
        }
        catch (Exception exception)
        {
            this.Logger.LogError(exception, "The SWOT category '{SwotCategory}' could not be created.", category);
            return new(SwotCategoryAnalysisStatus.FAILED, Message: T("This category could not be created."));
        }
    }

    private async Task FinalizeAnalysisAsync(
        IReadOnlyDictionary<SwotAnalysisCategory, SwotCategoryAnalysisResult> categoryResults,
        CancellationToken token)
    {
        this.isFinalizingAnalysis = true;
        await this.PublishAnalysisProgressAsync();

        try
        {
            var time = this.AddUserRequest(
                SwotAnalysisFinalization.BuildUserRequest(categoryResults),
                hideContentFromUser: true);
            var rawResponse = await this.AddAIResponseAsync(time, useSelectedTools: false);
            if (token.IsCancellationRequested || string.IsNullOrWhiteSpace(rawResponse))
                return;

            if (!SwotAnalysisFinalizationResponseParser.TryParse(rawResponse, categoryResults, out var finalization, out var issue))
            {
                this.Logger.LogWarning("The final answer of the SWOT analysis could not be read: {Issue}", issue);
                this.AddInputIssue(T("The prioritized actions could not be displayed because the model response did not have the expected structure."));
                return;
            }

            this.analysisResult = SwotAnalysisFinalization.Assemble(categoryResults, finalization);
            if (this.ResultingContentBlock?.Content is ContentText resultingText)
            {
                resultingText.Text = SwotAnalysisMarkdownFormatter.Format(this.analysisResult);
                resultingText.Presentation = SwotAnalysisContentPresentation.Create(this.analysisResult);
                await this.AddSharedResearchSourcesAsync(resultingText);
            }
        }
        finally
        {
            this.isFinalizingAnalysis = false;
            await this.PublishAnalysisProgressAsync();
        }
    }

    private async Task<string> GatherSharedResearchAsync(HashSet<string> runnableToolIds, CancellationToken token)
    {
        var userPrompt = new ContentText
        {
            Text = SwotSharedResearch.BuildUserRequest(
                this.inputText,
                this.analysisGoal,
                this.importantAspects,
                this.contextMaterials.Count > 0),
            FileAttachments = [.. this.contextMaterials],
        };
        var thread = new ChatThread
        {
            IncludeDateTime = false,
            SelectedProvider = this.ProviderSettings.Id,
            SelectedProfile = Profile.NO_PROFILE.Id,
            SelectedToolIds = [.. this.SelectedToolIds],
            SystemPrompt = SwotSharedResearch.BuildSystemPrompt(this.OutputLanguageInstruction),
            WorkspaceId = Guid.Empty,
            ChatId = Guid.NewGuid(),
            Name = $"{this.Title} - Shared Research",
            Blocks = [],
            RuntimeSelectedToolIds = [.. runnableToolIds],
            RuntimeToolsAreAssistantManaged = this.AssistantManagedToolIds is not null,
        };
        this.AssignRuntimeIdentity(thread);
        thread.Blocks.Add(CreateBlock(DateTimeOffset.Now, ChatRole.USER, userPrompt));

        var aiText = new ContentText { InitialRemoteWait = true };
        thread.Blocks.Add(CreateBlock(DateTimeOffset.Now, ChatRole.AI, aiText));

        try
        {
            await aiText.CreateFromProviderAsync(
                this.ProviderSettings.CreateProvider(),
                this.ProviderSettings.Model,
                userPrompt,
                thread,
                token);

            if (token.IsCancellationRequested)
                return string.Empty;

            if (!string.IsNullOrWhiteSpace(aiText.Text))
            {
                this.sharedResearchAnswer = aiText;
                this.sharedResearchChatId = thread.ChatId;
                return aiText.Text.Trim();
            }

            this.Logger.LogWarning("The shared research for the SWOT analysis returned no usable content.");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            this.Logger.LogWarning(exception, "The shared research for the SWOT analysis could not be created.");
        }
        finally
        {
            //
            // What the tools read tightened the research thread. The research reaches the result, and a
            // chat started from it, through the assistant's own thread, so that thread has to demand
            // the same. Also when the research failed: the tools may have read something before.
            //
            this.ChatThread?.RequireRestrictionsOf(thread);
        }

        await this.MessageBus.SendWarning(new(
            Icons.Material.Filled.CloudOff,
            T("The shared research could not be created. The SWOT analysis will continue without it.")));
        return string.Empty;
    }

    /// <summary>
    /// Adds the sources of the shared research to the result, together with what the AI received from them.
    /// </summary>
    /// <remarks>
    /// The research ran in a thread of its own, so its passages are stored under that thread's id.
    /// Copying them to the assistant's thread lets the result card and a chat started from it show
    /// what the analysis rests on.
    /// </remarks>
    /// <param name="result">The text of the result.</param>
    private async Task AddSharedResearchSourcesAsync(ContentText result)
    {
        if (this.sharedResearchAnswer is not { } research || this.ChatThread is null)
            return;

        result.AddSources(research.Sources, research.SourcePassages);
        await ChatProvenance.CopyChatAsync(this.sharedResearchChatId, this.ChatThread.ChatId);
    }

    private async Task PublishAnalysisProgressAsync()
    {
        await this.CheckpointAssistantSession();
        await this.RefreshAssistantUIAsync();
    }

    private static ContentBlock CreateBlock(DateTimeOffset time, ChatRole role, ContentText content) =>
        new()
        {
            Time = time,
            ContentType = ContentType.TEXT,
            Role = role,
            Content = content,
            HideFromUser = true,
        };
}