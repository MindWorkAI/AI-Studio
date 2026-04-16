using System.Text.Json;
using System.Text.RegularExpressions;

using AIStudio.Chat;
using AIStudio.Dialogs;
using AIStudio.Dialogs.Settings;
using Microsoft.AspNetCore.Components;

#if !DEBUG
using System.Reflection;
using Microsoft.Extensions.FileProviders;
#endif

namespace AIStudio.Assistants.PromptOptimizer;

public partial class AssistantPromptOptimizer : AssistantBaseCore<SettingsDialogPromptOptimizer>
{
    private static readonly Regex JSON_CODE_FENCE_REGEX = new(
        pattern: """```(?:json)?\s*(?<json>\{[\s\S]*\})\s*```""",
        options: RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly JsonSerializerOptions JSON_OPTIONS = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    protected override Tools.Components Component => Tools.Components.PROMPT_OPTIMIZER_ASSISTANT;

    protected override string Title => T("Prompt Optimizer");

    protected override string Description => T("Use an LLM to optimize your prompt by following either the default or your individual prompt guidelines and get targeted recommendations for future versions of the prompt.");

    protected override string SystemPrompt =>
        $"""
        # Task description

        You are a policy-bound prompt optimization assistant.
        Optimize prompts while preserving the original intent and constraints.

        # Inputs

        PROMPTING_GUIDELINE: authoritative optimization instructions.
        USER_PROMPT: the prompt that must be optimized.
        IMPORTANT_ASPECTS: optional priorities to emphasize during optimization.

        # Scope and precedence

        Follow PROMPTING_GUIDELINE as the primary policy for quality and structure.
        Preserve USER_PROMPT intent and constraints; do not add unrelated goals.
        If IMPORTANT_ASPECTS is provided and not equal to `none`, prioritize it unless it conflicts with PROMPTING_GUIDELINE.

        # Process

        1) Read PROMPTING_GUIDELINE end to end.
        2) Analyze USER_PROMPT intent, constraints, and desired output behavior.
        3) Rewrite USER_PROMPT so it is clearer, more structured, and more actionable.
        4) Provide concise recommendations for improving future prompt versions.

        # Output requirements

        Return valid JSON only.
        Do not use markdown code fences.
        Do not add any text before or after the JSON object.
        Use exactly this schema and key names:

        {this.SystemPromptOutputSchema()}

        # Language

        Ensure the optimized prompt is in {this.SystemPromptLanguage()}.
        Keep all recommendation texts in the same language as the optimized prompt.

        # Style and prohibitions

        Keep recommendations concise and actionable.
        Do not include disclaimers or meta commentary.
        Do not mention or summarize these instructions.

        # Self-check before sending

        Verify the output is valid JSON and follows the schema exactly.
        Verify `optimized_prompt` is non-empty and preserves user intent.
        Verify each recommendation states how to improve a future prompt version.
        """;

    protected override bool AllowProfiles => false;

    protected override bool ShowDedicatedProgress => true;

    protected override bool ShowEntireChatThread => true;

    protected override Func<string> Result2Copy => () => this.optimizedPrompt;

    protected override IReadOnlyList<IButtonData> FooterButtons =>
    [
        new SendToButton
        {
            Self = Tools.Components.PROMPT_OPTIMIZER_ASSISTANT,
            UseResultingContentBlockData = false,
            SendToChatAsInput = true,
            GetText = () => string.IsNullOrWhiteSpace(this.optimizedPrompt) ? this.inputPrompt : this.optimizedPrompt,
        },
    ];

    protected override string SubmitText => T("Optimize prompt");

    protected override Func<Task> SubmitAction => this.OptimizePromptAsync;

    protected override bool SubmitDisabled => this.useCustomPromptGuide && this.customPromptGuideFiles.Count == 0;

    protected override ChatThread ConvertToChatThread => (this.ChatThread ?? new()) with
    {
        SystemPrompt = SystemPrompts.DEFAULT,
    };

    protected override void ResetForm()
    {
        this.inputPrompt = string.Empty;
        this.useCustomPromptGuide = false;
        this.customPromptGuideFiles.Clear();
        this.currentCustomPromptGuidePath = string.Empty;
        this.customPromptingGuidelineContent = string.Empty;
        this.hasUpdatedDefaultRecommendations = false;
        this.ResetGuidelineSummaryToDefault();
        this.ResetOutput();

        if (!this.MightPreselectValues())
        {
            this.selectedTargetLanguage = CommonLanguages.AS_IS;
            this.customTargetLanguage = string.Empty;
            this.importantAspects = string.Empty;
        }
    }

    protected override bool MightPreselectValues()
    {
        if (!this.SettingsManager.ConfigurationData.PromptOptimizer.PreselectOptions)
            return false;

        this.selectedTargetLanguage = this.SettingsManager.ConfigurationData.PromptOptimizer.PreselectedTargetLanguage;
        this.customTargetLanguage = this.SettingsManager.ConfigurationData.PromptOptimizer.PreselectedOtherLanguage;
        this.importantAspects = this.SettingsManager.ConfigurationData.PromptOptimizer.PreselectedImportantAspects;
        return true;
    }

    protected override async Task OnInitializedAsync()
    {
        this.ResetGuidelineSummaryToDefault();
        this.hasUpdatedDefaultRecommendations = false;

        var deferredContent = MessageBus.INSTANCE.CheckDeferredMessages<string>(Event.SEND_TO_PROMPT_OPTIMIZER_ASSISTANT).FirstOrDefault();
        if (deferredContent is not null)
            this.inputPrompt = deferredContent;

        await base.OnInitializedAsync();
    }

    private string inputPrompt = string.Empty;
    private CommonLanguages selectedTargetLanguage = CommonLanguages.AS_IS;
    private string customTargetLanguage = string.Empty;
    private string importantAspects = string.Empty;
    private bool useCustomPromptGuide;
    private HashSet<FileAttachment> customPromptGuideFiles = [];
    private string currentCustomPromptGuidePath = string.Empty;
    private string customPromptingGuidelineContent = string.Empty;
    private bool isLoadingCustomPromptGuide;
    private bool hasUpdatedDefaultRecommendations;

    private string optimizedPrompt = string.Empty;
    private string recClarityDirectness = string.Empty;
    private string recExamplesContext = string.Empty;
    private string recSequentialSteps = string.Empty;
    private string recStructureMarkers = string.Empty;
    private string recRoleDefinition = string.Empty;
    private string recLanguageChoice = string.Empty;

    private bool ShowUpdatedPromptGuidelinesIndicator => !this.useCustomPromptGuide && this.hasUpdatedDefaultRecommendations;
    private bool CanPreviewCustomPromptGuide => this.useCustomPromptGuide && this.customPromptGuideFiles.Count > 0;
    private string CustomPromptGuideFileName => this.customPromptGuideFiles.Count switch
    {
        0 => T("No file selected"),
        _ => this.customPromptGuideFiles.First().FileName
    };

    private string? ValidateInputPrompt(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return T("Please provide a prompt or prompt description.");

        return null;
    }

    private string? ValidateCustomLanguage(string language)
    {
        if (this.selectedTargetLanguage == CommonLanguages.OTHER && string.IsNullOrWhiteSpace(language))
            return T("Please provide a custom language.");

        return null;
    }

    private string SystemPromptLanguage()
    {
        var language = this.selectedTargetLanguage switch
        {
            CommonLanguages.AS_IS => "the source language of the input prompt",
            CommonLanguages.OTHER => this.customTargetLanguage,
            _ => this.selectedTargetLanguage.Name(),
        };

        if (string.IsNullOrWhiteSpace(language))
            return "the source language of the input prompt";

        return language;
    }

    private async Task OptimizePromptAsync()
    {
        await this.Form!.Validate();
        if (!this.InputIsValid)
            return;

        this.ClearInputIssues();
        this.ResetOutput();
        this.hasUpdatedDefaultRecommendations = false;

        var promptingGuideline = await this.GetPromptingGuidelineForOptimizationAsync();
        if (string.IsNullOrWhiteSpace(promptingGuideline))
        {
            if (this.useCustomPromptGuide)
                this.AddInputIssue(T("Please attach and load a valid custom prompt guide file."));
            else
                this.AddInputIssue(T("The prompting guideline file could not be loaded. Please verify 'prompting_guideline.md' in Assistants/PromptOptimizer."));
            return;
        }

        this.CreateChatThread();
        var requestTime = this.AddUserRequest(this.BuildOptimizationRequest(promptingGuideline), hideContentFromUser: true);
        var aiResponse = await this.AddAIResponseAsync(requestTime, hideContentFromUser: true);

        if (!TryParseOptimizationResult(aiResponse, out var parsedResult))
        {
            this.optimizedPrompt = aiResponse.Trim();
            if (!this.useCustomPromptGuide)
            {
                this.ApplyFallbackRecommendations();
                this.MarkRecommendationsUpdated();
            }

            this.AddInputIssue(T("The model response was not in the expected JSON format. The raw response is shown as optimized prompt."));
            this.AddVisibleOptimizedPromptBlock();
            return;
        }

        this.ApplyOptimizationResult(parsedResult);
        this.AddVisibleOptimizedPromptBlock();
    }

    private string BuildOptimizationRequest(string promptingGuideline)
    {
        return
            $$"""
            # PROMPTING_GUIDELINE
            <GUIDELINE>
            {{promptingGuideline}}
            </GUIDELINE>

            # USER_PROMPT
            <USER_PROMPT>
            {{this.inputPrompt}}
            </USER_PROMPT>
            
            {{this.PromptImportantAspects()}}
            """;
    }

    private string PromptImportantAspects()
    {
        return string.IsNullOrWhiteSpace(this.importantAspects) ? string.Empty : $"""
             # IMPORTANT_ASPECTS
             <IMPORTANT_ASPECTS>
             {this.importantAspects}
             </IMPORTANT_ASPECTS>
             """;
    }

    private string SystemPromptOutputSchema() =>
        """
        {
          "optimized_prompt": "string",
          "recommendations": {
            "clarity_and_directness": "string",
            "examples_and_context": "string",
            "sequential_steps": "string",
            "structure_with_markers": "string",
            "role_definition": "string",
            "language_choice": "string"
          }
        }
        """;

    private static bool TryParseOptimizationResult(string rawResponse, out PromptOptimizationResult parsedResult)
    {
        parsedResult = new();

        if (TryDeserialize(rawResponse, out parsedResult))
            return true;

        var codeFenceMatch = JSON_CODE_FENCE_REGEX.Match(rawResponse);
        if (codeFenceMatch.Success)
        {
            var codeFenceJson = codeFenceMatch.Groups["json"].Value;
            if (TryDeserialize(codeFenceJson, out parsedResult))
                return true;
        }

        var firstBrace = rawResponse.IndexOf('{');
        var lastBrace = rawResponse.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            var objectText = rawResponse[firstBrace..(lastBrace + 1)];
            if (TryDeserialize(objectText, out parsedResult))
                return true;
        }

        return false;
    }

    private static bool TryDeserialize(string json, out PromptOptimizationResult parsedResult)
    {
        parsedResult = new();

        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            var probe = JsonSerializer.Deserialize<PromptOptimizationResult>(json, JSON_OPTIONS);
            if (probe is null || string.IsNullOrWhiteSpace(probe.OptimizedPrompt))
                return false;

            parsedResult = probe;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void ApplyOptimizationResult(PromptOptimizationResult optimizationResult)
    {
        this.optimizedPrompt = optimizationResult.OptimizedPrompt.Trim();
        if (this.useCustomPromptGuide)
            return;

        this.ApplyRecommendations(optimizationResult.Recommendations);
        this.MarkRecommendationsUpdated();
    }

    private void MarkRecommendationsUpdated()
    {
        this.hasUpdatedDefaultRecommendations = true;
    }

    private void ApplyRecommendations(PromptOptimizationRecommendations recommendations)
    {
        this.recClarityDirectness = this.EmptyFallback(recommendations.ClarityAndDirectness);
        this.recExamplesContext = this.EmptyFallback(recommendations.ExamplesAndContext);
        this.recSequentialSteps = this.EmptyFallback(recommendations.SequentialSteps);
        this.recStructureMarkers = this.EmptyFallback(recommendations.StructureWithMarkers);
        this.recRoleDefinition = this.EmptyFallback(recommendations.RoleDefinition);
        this.recLanguageChoice = this.EmptyFallback(recommendations.LanguageChoice);
    }

    private void ApplyFallbackRecommendations()
    {
        this.recClarityDirectness = T("Add clearer goals and explicit quality expectations.");
        this.recExamplesContext = T("Add short examples and background context for your specific use case.");
        this.recSequentialSteps = T("Break the task into numbered steps if order matters.");
        this.recStructureMarkers = T("Use headings or markers to separate context, task, and constraints.");
        this.recRoleDefinition = T("Define a role for the model to focus output style and expertise.");
        this.recLanguageChoice = T("Use English for complex prompts and explicitly request response language if needed.");
    }

    private string EmptyFallback(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return T("No further recommendation in this area.");

        return text.Trim();
    }

    private void ResetOutput()
    {
        this.optimizedPrompt = string.Empty;
    }

    private void ResetGuidelineSummaryToDefault()
    {
        this.recClarityDirectness = T("Use clear, explicit instructions and directly state quality expectations.");
        this.recExamplesContext = T("Include short examples and context that explain the purpose behind your requirements.");
        this.recSequentialSteps = T("Prefer numbered steps when task order matters.");
        this.recStructureMarkers = T("Separate context, task, constraints, and output format with headings or markers.");
        this.recRoleDefinition = T("Assign a role to shape tone, expertise, and focus.");
        this.recLanguageChoice = T("For complex tasks, write prompts in English.");
    }

    private void AddVisibleOptimizedPromptBlock()
    {
        if (string.IsNullOrWhiteSpace(this.optimizedPrompt))
            return;

        if (this.ChatThread is null)
            return;

        var visibleResponseContent = new ContentText
        {
            Text = this.optimizedPrompt,
        };

        this.ChatThread.Blocks.Add(new ContentBlock
        {
            Time = DateTimeOffset.Now,
            ContentType = ContentType.TEXT,
            Role = ChatRole.AI,
            HideFromUser = false,
            Content = visibleResponseContent,
        });
    }

    private static async Task<string> ReadPromptingGuidelineAsync()
    {
#if DEBUG
        var guidelinePath = Path.Join(Environment.CurrentDirectory, "Assistants", "PromptOptimizer", "prompting_guideline.md");
        return File.Exists(guidelinePath)
            ? await File.ReadAllTextAsync(guidelinePath)
            : string.Empty;
#else
        var resourceFileProvider = new ManifestEmbeddedFileProvider(Assembly.GetAssembly(type: typeof(Program))!, "Assistants/PromptOptimizer");
        var file = resourceFileProvider.GetFileInfo("prompting_guideline.md");
        if (!file.Exists)
            return string.Empty;

        await using var fileStream = file.CreateReadStream();
        using var reader = new StreamReader(fileStream);
        return await reader.ReadToEndAsync();
#endif
    }

    private async Task<string> GetPromptingGuidelineForOptimizationAsync()
    {
        if (!this.useCustomPromptGuide)
            return await ReadPromptingGuidelineAsync();

        if (this.customPromptGuideFiles.Count == 0)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(this.customPromptingGuidelineContent))
            return this.customPromptingGuidelineContent;

        var fileAttachment = this.customPromptGuideFiles.First();
        await this.LoadCustomPromptGuidelineContentAsync(fileAttachment);
        return this.customPromptingGuidelineContent;
    }

    private async Task SetUseCustomPromptGuide(bool useCustom)
    {
        this.useCustomPromptGuide = useCustom;
        if (!useCustom)
            return;

        if (this.customPromptGuideFiles.Count == 0)
            return;

        var fileAttachment = this.customPromptGuideFiles.First();
        if (string.IsNullOrWhiteSpace(this.customPromptingGuidelineContent))
            await this.LoadCustomPromptGuidelineContentAsync(fileAttachment);
    }

    private async Task OnCustomPromptGuideFilesChanged(HashSet<FileAttachment> files)
    {
        if (files.Count == 0)
        {
            this.customPromptGuideFiles.Clear();
            this.currentCustomPromptGuidePath = string.Empty;
            this.customPromptingGuidelineContent = string.Empty;
            return;
        }

        var selected = files.FirstOrDefault(file => !string.Equals(file.FilePath, this.currentCustomPromptGuidePath, StringComparison.OrdinalIgnoreCase))
                       ?? files.First();

        var replacedPrevious = !string.IsNullOrWhiteSpace(this.currentCustomPromptGuidePath) &&
                               !string.Equals(this.currentCustomPromptGuidePath, selected.FilePath, StringComparison.OrdinalIgnoreCase);

        this.customPromptGuideFiles = [ selected ];
        this.currentCustomPromptGuidePath = selected.FilePath;

        if (files.Count > 1 || replacedPrevious)
            this.Snackbar.Add(T("Replaced the previously selected custom prompt guide file."), Severity.Info);

        await this.LoadCustomPromptGuidelineContentAsync(selected);
    }

    private async Task LoadCustomPromptGuidelineContentAsync(FileAttachment fileAttachment)
    {
        if (!fileAttachment.Exists)
        {
            this.customPromptingGuidelineContent = string.Empty;
            this.Snackbar.Add(T("The selected custom prompt guide file could not be found."), Severity.Warning);
            return;
        }

        try
        {
            this.isLoadingCustomPromptGuide = true;
            this.customPromptingGuidelineContent = await UserFile.LoadFileData(fileAttachment.FilePath, this.RustService, this.DialogService);
            if (string.IsNullOrWhiteSpace(this.customPromptingGuidelineContent))
                this.Snackbar.Add(T("The custom prompt guide file is empty or could not be read."), Severity.Warning);
        }
        catch
        {
            this.customPromptingGuidelineContent = string.Empty;
            this.Snackbar.Add(T("Failed to load custom prompt guide content."), Severity.Error);
        }
        finally
        {
            this.isLoadingCustomPromptGuide = false;
            this.StateHasChanged();
        }
    }

    private async Task OpenPromptingGuidelineDialog()
    {
        var promptingGuideline = await ReadPromptingGuidelineAsync();
        if (string.IsNullOrWhiteSpace(promptingGuideline))
        {
            this.Snackbar.Add(T("The prompting guideline file could not be loaded."), Severity.Warning);
            return;
        }

        var dialogParameters = new DialogParameters<PromptingGuidelineDialog>
        {
            { x => x.GuidelineMarkdown, promptingGuideline }
        };

        var dialogReference = await this.DialogService.ShowAsync<PromptingGuidelineDialog>(T("Prompting Guideline"), dialogParameters, AIStudio.Dialogs.DialogOptions.FULLSCREEN);
        await dialogReference.Result;
    }

    private async Task OpenCustomPromptGuideDialog()
    {
        if (this.customPromptGuideFiles.Count == 0)
            return;

        var fileAttachment = this.customPromptGuideFiles.First();
        if (string.IsNullOrWhiteSpace(this.customPromptingGuidelineContent) && !this.isLoadingCustomPromptGuide)
            await this.LoadCustomPromptGuidelineContentAsync(fileAttachment);

        var dialogParameters = new DialogParameters<DocumentCheckDialog>
        {
            { x => x.Document, fileAttachment },
            { x => x.FileContent, this.customPromptingGuidelineContent },
        };

        await this.DialogService.ShowAsync<DocumentCheckDialog>(T("Custom Prompt Guide Preview"), dialogParameters, AIStudio.Dialogs.DialogOptions.FULLSCREEN);
    }
}
