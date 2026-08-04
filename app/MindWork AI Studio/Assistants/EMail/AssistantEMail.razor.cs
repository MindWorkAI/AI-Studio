using System.Text;

using AIStudio.Dialogs.Settings;
using AIStudio.Tools.AssistantSessions;

namespace AIStudio.Assistants.EMail;

public partial class AssistantEMail : AssistantBaseCore<SettingsDialogWritingEMails>
{
    protected override Tools.Components Component => Tools.Components.EMAIL_ASSISTANT;
    
    protected override string Title => T("E-Mail");
    
    protected override string Description => T("Provide a list of bullet points and some basic information for an e-mail. The assistant will generate an e-mail based on that input.");
    
    protected override string SystemPrompt => 
        $"""
        You are an automated system that writes emails. {this.SystemPromptHistory()} The user provides you with bullet points on what
        he want to address in the response. Regarding the writing style of the email: {this.selectedWritingStyle.Prompt()}
        {this.SystemPromptGreeting()} {this.SystemPromptName()} You write the email in the following language: {this.SystemPromptLanguage()}.
        """;
    
    protected override IReadOnlyList<IButtonData> FooterButtons => [];
    
    protected override string SubmitText => T("Create email");

    protected override Func<Task> SubmitAction => this.CreateMail;

    protected override string SendToChatVisibleUserPromptPrefix => T("Create an email based on the following bullet points:");

    protected override string SendToChatVisibleUserPromptContent => this.inputBulletPoints;
    
    protected override void ResetForm()
    {
        this.inputBulletPoints = string.Empty;
        this.bulletPointsLines.Clear();
        this.selectedFoci = [];
        this.provideHistory = false;
        this.inputHistory = string.Empty;
        if (!this.MightPreselectValues())
        {
            this.inputName = string.Empty;
            this.selectedTargetLanguage = CommonLanguages.AS_IS;
            this.customTargetLanguage = string.Empty;
            this.selectedWritingStyle = WritingStyles.NONE;
            this.inputGreeting = string.Empty;
        }
    }
    
    protected override bool MightPreselectValues()
    {
        if (this.SettingsManager.ConfigurationData.EMail.PreselectOptions)
        {
            this.inputName = this.SettingsManager.ConfigurationData.EMail.SenderName;
            this.inputGreeting = this.SettingsManager.ConfigurationData.EMail.Greeting;
            this.selectedWritingStyle = this.SettingsManager.ConfigurationData.EMail.PreselectedWritingStyle;
            this.selectedTargetLanguage = this.SettingsManager.ConfigurationData.EMail.PreselectedTargetLanguage;
            this.customTargetLanguage = this.SettingsManager.ConfigurationData.EMail.PreselectOtherLanguage;
            return true;
        }
        
        return false;
    }
    
    private const string PLACEHOLDER_BULLET_POINTS = """
                                               - The last meeting was good
                                               - Thank you for feedback
                                               - Next is milestone 3
                                               - I need your input by next Wednesday
                                               """;
    
    private WritingStyles selectedWritingStyle = WritingStyles.NONE;
    private string inputGreeting = string.Empty;
    private string inputBulletPoints = string.Empty;
    private readonly List<string> bulletPointsLines = [];
    private IEnumerable<string> selectedFoci = new HashSet<string>();
    private string inputName = string.Empty;
    private CommonLanguages selectedTargetLanguage = CommonLanguages.AS_IS;
    private string customTargetLanguage = string.Empty;
    private bool provideHistory;
    private string inputHistory = string.Empty;
    private static readonly AssistantSessionStateKey<WritingStyles> SELECTED_WRITING_STYLE_STATE_KEY = new(nameof(selectedWritingStyle));
    private static readonly AssistantSessionStateKey<string> INPUT_GREETING_STATE_KEY = new(nameof(inputGreeting));
    private static readonly AssistantSessionStateKey<string> INPUT_BULLET_POINTS_STATE_KEY = new(nameof(inputBulletPoints));
    private static readonly AssistantSessionStateKey<List<string>> BULLET_POINTS_LINES_STATE_KEY = new(nameof(bulletPointsLines));
    private static readonly AssistantSessionStateKey<HashSet<string>> SELECTED_FOCI_STATE_KEY = new(nameof(selectedFoci));
    private static readonly AssistantSessionStateKey<string> INPUT_NAME_STATE_KEY = new(nameof(inputName));
    private static readonly AssistantSessionStateKey<CommonLanguages> SELECTED_TARGET_LANGUAGE_STATE_KEY = new(nameof(selectedTargetLanguage));
    private static readonly AssistantSessionStateKey<string> CUSTOM_TARGET_LANGUAGE_STATE_KEY = new(nameof(customTargetLanguage));
    private static readonly AssistantSessionStateKey<bool> PROVIDE_HISTORY_STATE_KEY = new(nameof(provideHistory));
    private static readonly AssistantSessionStateKey<string> INPUT_HISTORY_STATE_KEY = new(nameof(inputHistory));

    /// <inheritdoc />
    protected override void CaptureCustomAssistantSessionState(AssistantSessionStateWriter state)
    {
        state.Set(SELECTED_WRITING_STYLE_STATE_KEY, this.selectedWritingStyle);
        state.Set(INPUT_GREETING_STATE_KEY, this.inputGreeting);
        state.Set(INPUT_BULLET_POINTS_STATE_KEY, this.inputBulletPoints);
        state.SetList(BULLET_POINTS_LINES_STATE_KEY, this.bulletPointsLines);
        state.SetHashSet(SELECTED_FOCI_STATE_KEY, this.selectedFoci);
        state.Set(INPUT_NAME_STATE_KEY, this.inputName);
        state.Set(SELECTED_TARGET_LANGUAGE_STATE_KEY, this.selectedTargetLanguage);
        state.Set(CUSTOM_TARGET_LANGUAGE_STATE_KEY, this.customTargetLanguage);
        state.Set(PROVIDE_HISTORY_STATE_KEY, this.provideHistory);
        state.Set(INPUT_HISTORY_STATE_KEY, this.inputHistory);
    }

    /// <inheritdoc />
    protected override void RestoreCustomAssistantSessionState(AssistantSessionStateReader state)
    {
        state.Restore(SELECTED_WRITING_STYLE_STATE_KEY, value => this.selectedWritingStyle = value);
        state.Restore(INPUT_GREETING_STATE_KEY, value => this.inputGreeting = value);
        state.Restore(INPUT_BULLET_POINTS_STATE_KEY, value => this.inputBulletPoints = value);
        state.RestoreList(BULLET_POINTS_LINES_STATE_KEY, this.bulletPointsLines);
        state.Restore(SELECTED_FOCI_STATE_KEY, value => this.selectedFoci = value);
        state.Restore(INPUT_NAME_STATE_KEY, value => this.inputName = value);
        state.Restore(SELECTED_TARGET_LANGUAGE_STATE_KEY, value => this.selectedTargetLanguage = value);
        state.Restore(CUSTOM_TARGET_LANGUAGE_STATE_KEY, value => this.customTargetLanguage = value);
        state.Restore(PROVIDE_HISTORY_STATE_KEY, value => this.provideHistory = value);
        state.Restore(INPUT_HISTORY_STATE_KEY, value => this.inputHistory = value);
    }
    
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        var deferredContent = MessageBus.INSTANCE.CheckDeferredMessages<string>(Event.SEND_TO_EMAIL_ASSISTANT).FirstOrDefault();
        if (deferredContent is not null)
            this.inputBulletPoints = deferredContent;
        
        await base.OnInitializedAsync();
    }

    #endregion
    
    private string? ValidateBulletPoints(string content)
    {
        if(string.IsNullOrWhiteSpace(content))
            return T("Please provide some content for the e-mail.");

        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
            if(!line.TrimStart().StartsWith('-'))
                return T("Please start each line of your content list with a dash (-) to create a bullet point list.");
        
        return null;
    }
    
    private string? ValidateTargetLanguage(CommonLanguages language)
    {
        if(language is CommonLanguages.AS_IS)
            return T("Please select a target language for the e-mail.");
        
        return null;
    }
    
    private string? ValidateCustomLanguage(string language)
    {
        if(this.selectedTargetLanguage == CommonLanguages.OTHER && string.IsNullOrWhiteSpace(language))
            return T("Please provide a custom language.");
        
        return null;
    }
    
    private string? ValidateWritingStyle(WritingStyles style)
    {
        if(style == WritingStyles.NONE)
            return T("Please select a writing style for the e-mail.");
        
        return null;
    }
    
    private string? ValidateHistory(string history)
    {
        if(this.provideHistory && string.IsNullOrWhiteSpace(history))
            return T("Please provide some history for the e-mail.");
        
        return null;
    }
    
    private void OnContentChanged(string content)
    {
        this.bulletPointsLines.Clear();
        var previousSelectedFoci = new HashSet<string>();
        foreach (var line in content.AsSpan().EnumerateLines())
        {
            var trimmedLine = line.Trim();
            if (trimmedLine.StartsWith("-"))
                trimmedLine = trimmedLine[1..].Trim();
            
            if (trimmedLine.Length == 0)
                continue;
            
            var finalLine = trimmedLine.ToString();
            if(this.selectedFoci.Any(x => x.StartsWith(finalLine, StringComparison.InvariantCultureIgnoreCase)))
                previousSelectedFoci.Add(finalLine);
            
            this.bulletPointsLines.Add(finalLine);
        }

        this.selectedFoci = previousSelectedFoci;
    }

    private string SystemPromptHistory()
    {
        if (this.provideHistory)
            return "You receive the previous conversation as context.";
        
        return string.Empty;
    }

    private string SystemPromptGreeting()
    {
        if(!string.IsNullOrWhiteSpace(this.inputGreeting))
            return $"Your greeting should consider the following formulation: {this.inputGreeting}.";
        
        return string.Empty;
    }

    private string SystemPromptName()
    {
        if(!string.IsNullOrWhiteSpace(this.inputName))
            return $"For the closing phrase of the email, please use the following name: {this.inputName}.";
        
        return string.Empty;
    }

    private string SystemPromptLanguage()
    {
        if(this.selectedTargetLanguage is CommonLanguages.AS_IS)
            return "Use the same language as the input";
        
        if(this.selectedTargetLanguage is CommonLanguages.OTHER)
            return this.customTargetLanguage;
        
        return this.selectedTargetLanguage.Name();
    }
    
    private string PromptFoci()
    {
        if(!this.selectedFoci.Any())
            return string.Empty;
        
        var sb = new StringBuilder();
        sb.AppendLine("I want to amplify the following points:");
        foreach (var focus in this.selectedFoci)
            sb.AppendLine($"- {focus}");
        
        return sb.ToString();
    }
    
    private string PromptHistory()
    {
        if(!this.provideHistory)
            return string.Empty;
        
        return $"""
               The previous conversation was:
               
               ```
               {this.inputHistory}
               ```
               """;
    }

    private async Task CreateMail()
    {
        await this.Form!.Validate();
        if (!this.InputIsValid)
            return;

        this.CreateChatThread();
        var time = this.AddUserRequest(
            $"""
            {this.PromptHistory()}
            
            My bullet points for the e-mail are:
            
            {this.inputBulletPoints}
            
            {this.PromptFoci()}
            """);

        await this.AddAIResponseAsync(time);
    }
}