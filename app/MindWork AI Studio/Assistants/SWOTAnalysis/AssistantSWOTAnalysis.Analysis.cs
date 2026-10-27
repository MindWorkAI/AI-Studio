using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Rust;

namespace AIStudio.Assistants.SWOTAnalysis;

public partial class AssistantSWOTAnalysis
{
    /// <summary>
    /// Runs the whole analysis: the shared research, the four categories, and the final request.
    /// </summary>
    /// <remarks>
    /// The contextual knowledge is read once, before anything else, and every request gets it from
    /// there. The research runs once, with the selected tools, so the four categories need no tools
    /// and run at the same time, each in a thread of its own. The user sees none of these threads;
    /// only the final request goes through the assistant's thread, whose answer becomes the result.
    /// After each step, a checkpoint lets an instance the user returns to show how far the analysis is.
    /// </remarks>
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
        this.isModeratingAnalysis = false;
        this.isRepairingModeration = false;
        this.categoryAnalysisProgress.Clear();
        this.CreateChatThread();

        var token = this.CancellationTokenSource?.Token ?? CancellationToken.None;
        var contextualKnowledge = await this.PrepareContextualKnowledgeAsync(token);
        if (token.IsCancellationRequested)
            return;

        // The research runs in a thread of its own, which has read nothing yet:
        var runnableToolIds = this.GetRunnableToolIds(OutboundDataRestriction.UNRESTRICTED);
        if (runnableToolIds.Count > 0)
        {
            this.isGatheringSharedResearch = true;
            await this.PublishAnalysisProgressAsync();
            try
            {
                this.sharedResearchContext = await this.GatherSharedResearchAsync(runnableToolIds, contextualKnowledge, token);
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
            category => this.AnalyzeCategoryAsync(category, contextualKnowledge, token),
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

        await this.ModerateAnalysisAsync(categoryResults, token);
    }

    private async Task<SwotCategoryAnalysisProgress> AnalyzeCategoryAsync(SwotAnalysisCategory category, SwotContextualKnowledge contextualKnowledge, CancellationToken token)
    {
        var request = SwotCategoryAnalysis.BuildUserRequest(this.inputText, this.analysisGoal, this.importantAspects, contextualKnowledge, this.sharedResearchContext);
        var thread = this.CreatePrivateThread($"{this.Title} - {category}", SwotCategoryAnalysis.BuildSystemPrompt(category, this.OutputLanguageInstruction), []);

        try
        {
            var answer = await this.AskInPrivateThreadAsync(thread, request, contextualKnowledge.Images, token);
            if (token.IsCancellationRequested)
                return new(SwotCategoryAnalysisStatus.CANCELED, Message: T("The category analysis was canceled."));

            var read = await SwotModelAnswer.ReadWithOneRepairAsync<SwotCategoryAnalysisResult>(answer.Text, SwotCategoryAnalysisResponseParser.TryParse, async issue =>
            {
                this.Logger.LogWarning("The answer for the SWOT category '{SwotCategory}' could not be read, so the model is asked once more: {Issue}", category, issue);
                this.categoryAnalysisProgress[category] = new(SwotCategoryAnalysisStatus.REPAIRING);
                await this.PublishAnalysisProgressAsync();

                // In the same thread, so the model sees its own answer; the images went along with the first request:
                var repairedAnswer = await this.AskInPrivateThreadAsync(thread, SwotModelAnswer.RepairRequestOf(issue), [], token);
                return repairedAnswer.Text;
            });

            if (token.IsCancellationRequested)
                return new(SwotCategoryAnalysisStatus.CANCELED, Message: T("The category analysis was canceled."));

            if (read.Result is null)
            {
                this.Logger.LogWarning("The answer for the SWOT category '{SwotCategory}' could not be read: {Issue}", category, read.Issue);
                return new(SwotCategoryAnalysisStatus.FAILED, Message: T("The answer for this category did not have the expected form, even after asking the model once more."));
            }

            return new(SwotCategoryAnalysisStatus.COMPLETED, read.Result);
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

    /// <summary>
    /// Lets the moderator reconcile the four categories, and makes its answer the result.
    /// </summary>
    /// <remarks>
    /// The moderator runs in the assistant's own thread, so its answer becomes the result card and goes
    /// along to a chat. It runs without tools, see SwotAnalysisModeration. Its answer is JSON and stays
    /// hidden while it streams; the result shows the analysis assembled from it. An answer which cannot
    /// be read gets one repair round in the same thread, see SwotModelAnswer.ReadWithOneRepairAsync;
    /// the repaired answer then becomes the result, and a chat gets neither the repair request nor the
    /// first answer, see ConvertToChatThread.
    /// </remarks>
    private async Task ModerateAnalysisAsync(
        IReadOnlyDictionary<SwotAnalysisCategory, SwotCategoryAnalysisResult> categoryResults,
        CancellationToken token)
    {
        this.isModeratingAnalysis = true;
        await this.PublishAnalysisProgressAsync();

        try
        {
            var time = this.AddUserRequest(
                SwotAnalysisModeration.BuildUserRequest(categoryResults, this.analysisGoal, this.importantAspects, this.sharedResearchContext),
                hideContentFromUser: true);
            var rawResponse = await this.AddAIResponseAsync(time, hideContentFromUser: true, useSelectedTools: false);
            if (token.IsCancellationRequested || string.IsNullOrWhiteSpace(rawResponse))
                return;

            var read = await SwotModelAnswer.ReadWithOneRepairAsync<SwotModerationResult>(rawResponse, SwotAnalysisModerationResponseParser.TryParse, this.AskModeratorOnceMoreAsync);
            if (token.IsCancellationRequested)
                return;

            if (read.Result is null)
            {
                this.Logger.LogWarning("The answer of the SWOT analysis moderator could not be read: {Issue}", read.Issue);
                this.AddInputIssue(T("The four SWOT categories could not be reconciled because the answer did not have the expected form, even after asking the model once more. The preliminary results of the categories remain visible below."));
                return;
            }

            this.analysisResult = SwotAnalysisModeration.Assemble(read.Result);
            if (this.ResultingContentBlock?.Content is ContentText resultingText)
            {
                resultingText.Text = SwotAnalysisMarkdownFormatter.Format(this.analysisResult);
                resultingText.Presentation = SwotAnalysisContentPresentation.Create(this.analysisResult);
                await this.AddSharedResearchSourcesAsync(resultingText);
            }
        }
        finally
        {
            this.isModeratingAnalysis = false;
            this.isRepairingModeration = false;
            await this.PublishAnalysisProgressAsync();
        }
    }

    /// <summary>
    /// Asks the moderator once more, in the assistant's thread, for an answer in the expected form.
    /// </summary>
    /// <param name="issue">What is wrong with the first answer.</param>
    /// <returns>The new answer, which is empty when the provider failed.</returns>
    private async Task<string> AskModeratorOnceMoreAsync(string issue)
    {
        this.Logger.LogWarning("The answer of the SWOT analysis moderator could not be read, so the model is asked once more: {Issue}", issue);
        this.isRepairingModeration = true;
        await this.PublishAnalysisProgressAsync();

        var time = this.AddUserRequest(SwotModelAnswer.RepairRequestOf(issue), hideContentFromUser: true);
        return await this.AddAIResponseAsync(time, hideContentFromUser: true, useSelectedTools: false);
    }

    private async Task<string> GatherSharedResearchAsync(HashSet<string> runnableToolIds, SwotContextualKnowledge contextualKnowledge, CancellationToken token)
    {
        var request = SwotSharedResearch.BuildUserRequest(this.inputText, this.analysisGoal, this.importantAspects, contextualKnowledge);
        var thread = this.CreatePrivateThread($"{this.Title} - Shared Research", SwotSharedResearch.BuildSystemPrompt(this.OutputLanguageInstruction), runnableToolIds);

        try
        {
            var answer = await this.AskInPrivateThreadAsync(thread, request, contextualKnowledge.Images, token);
            if (token.IsCancellationRequested)
                return string.Empty;

            if (!string.IsNullOrWhiteSpace(answer.Text))
            {
                this.sharedResearchAnswer = answer;
                this.sharedResearchChatId = thread.ChatId;
                return answer.Text.Trim();
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
            T("The research with the selected tools failed. The SWOT analysis continues without it.")));
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

    /// <summary>
    /// Reads the documents and prepares the images of the contextual knowledge, once for all requests.
    /// </summary>
    /// <remarks>
    /// One prompt injection report for all documents, closed right after reading, so the user hears
    /// about them before the analysis goes on, see SwotContextualKnowledge. An image which cannot be
    /// prepared is reported here and left out; the requests find the others in the cache of
    /// ModelImageService.
    /// </remarks>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The prepared contextual knowledge.</returns>
    private async Task<SwotContextualKnowledge> PrepareContextualKnowledgeAsync(CancellationToken token)
    {
        if (this.contextMaterials.Count == 0)
            return SwotContextualKnowledge.NONE;

        var documents = new ContentText { FileAttachments = this.contextMaterials.Where(material => !material.IsImage).ToList() };
        var documentText = string.Empty;
        if (documents.FileAttachments.Count > 0)
        {
            await using var promptInjectionScope = this.PromptInjectionGuard.BeginAction();
            documentText = (await documents.PrepareTextContentForAI(promptInjectionScope)).Trim();
        }

        var images = new List<FileAttachment>();
        var imageCandidates = this.contextMaterials.OfType<FileAttachmentImage>().ToList();
        if (imageCandidates.Count > 0 && !this.ProviderSettings.SupportsImageInput())
        {
            await this.MessageBus.SendWarning(new(Icons.Material.Filled.HideImage, T("The selected model cannot read images. The SWOT analysis continues without the attached images.")));
            return new(documentText, images);
        }

        foreach (var image in imageCandidates)
        {
            try
            {
                var prepared = await this.ModelImageService.PrepareAsync(image, this.ProviderSettings, token);
                if (prepared.IsPrepared)
                {
                    images.Add(image);
                    continue;
                }

                await this.MessageBus.SendError(new(Icons.Material.Filled.BrokenImage, prepared.ErrorCode.ToUserMessage(image.FileName)));
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                this.Logger.LogError(exception, "The image '{FilePath}' could not be prepared for the SWOT analysis.", image.FilePath);
                await this.MessageBus.SendError(new(Icons.Material.Filled.BrokenImage, string.Format(T("The image '{0}' could not be read and is left out of the SWOT analysis."), image.FileName)));
            }
        }

        return new(documentText, images);
    }

    /// <summary>
    /// Creates a thread of its own for one request of the analysis, which the user never sees.
    /// </summary>
    /// <param name="name">The name of the thread, for the log.</param>
    /// <param name="systemPrompt">The system prompt of the request.</param>
    /// <param name="runnableToolIds">The tools the model may use; empty for none.</param>
    /// <returns>The thread, without any blocks yet.</returns>
    private ChatThread CreatePrivateThread(string name, string systemPrompt, HashSet<string> runnableToolIds)
    {
        var thread = new ChatThread
        {
            IncludeDateTime = this.IncludeDateTime,
            SelectedProvider = this.ProviderSettings.Id,
            SelectedProfile = Profile.NO_PROFILE.Id,
            SelectedToolIds = [.. this.SelectedToolIds],
            SystemPrompt = systemPrompt,
            WorkspaceId = Guid.Empty,
            ChatId = Guid.NewGuid(),
            Name = name,
            Blocks = [],
            RuntimeSelectedToolIds = [.. runnableToolIds],
            RuntimeToolsAreAssistantManaged = this.AssistantManagedToolIds is not null,
        };

        this.AssignRuntimeIdentity(thread);
        return thread;
    }

    /// <summary>
    /// Sends a request in a private thread and waits for the whole answer.
    /// </summary>
    /// <param name="thread">The private thread.</param>
    /// <param name="request">The request.</param>
    /// <param name="attachments">The files to attach to the request.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The answer, which is empty when the provider failed.</returns>
    private async Task<ContentText> AskInPrivateThreadAsync(ChatThread thread, string request, IReadOnlyList<FileAttachment> attachments, CancellationToken token)
    {
        var userPrompt = new ContentText
        {
            Text = request,
            FileAttachments = [.. attachments],
        };

        thread.Blocks.Add(CreateBlock(DateTimeOffset.Now, ChatRole.USER, userPrompt));
        var answer = new ContentText { InitialRemoteWait = true };
        thread.Blocks.Add(CreateBlock(DateTimeOffset.Now, ChatRole.AI, answer));

        await answer.CreateFromProviderAsync(this.ProviderSettings.CreateProvider(), this.ProviderSettings.Model, userPrompt, thread, token);
        return answer;
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