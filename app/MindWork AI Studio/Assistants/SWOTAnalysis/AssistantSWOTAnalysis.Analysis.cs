using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;

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
        this.isFinalizingAnalysis = false;
        this.CreateChatThread();

        var categories = Enum.GetValues<SwotAnalysisCategory>();
        this.categoryAnalysisProgress = categories.ToDictionary(
            category => category,
            _ => new SwotCategoryAnalysisProgress(SwotCategoryAnalysisStatus.RUNNING));
        await this.PublishAnalysisProgressAsync();

        var token = this.CancellationTokenSource?.Token ?? CancellationToken.None;
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
                this.contextMaterials.Count > 0),
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
            RuntimeComponent = this.Component,
            RuntimeSelectedToolIds = this.GetRunnableToolIds(),
            RuntimeToolsAreAssistantManaged = this.AssistantManagedToolIds is not null,
        };
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

            if (!SwotCategoryAnalysisResponseParser.TryParse(aiText.Text, out var result))
                return new(SwotCategoryAnalysisStatus.FAILED, Message: T("The model response for this category did not have the expected structure."));

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
            var rawResponse = await this.AddAIResponseAsync(time, hideContentFromUser: true);
            if (token.IsCancellationRequested || string.IsNullOrWhiteSpace(rawResponse))
                return;

            if (!SwotAnalysisFinalizationResponseParser.TryParse(rawResponse, categoryResults, out var finalization))
            {
                this.AddInputIssue(T("The prioritized actions could not be displayed because the model response did not have the expected structure."));
                return;
            }

            this.analysisResult = SwotAnalysisFinalization.Assemble(categoryResults, finalization);
            if (this.ResultingContentBlock?.Content is ContentText resultingText)
                resultingText.Text = SwotAnalysisMarkdownFormatter.Format(this.analysisResult);
        }
        finally
        {
            this.isFinalizingAnalysis = false;
            await this.PublishAnalysisProgressAsync();
        }
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