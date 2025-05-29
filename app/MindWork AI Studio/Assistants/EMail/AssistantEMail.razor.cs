using System.Text;

using AIStudio.Chat;
using AIStudio.Dialogs.Settings;

namespace AIStudio.Assistants.EMail;

public partial class AssistantEMail : AssistantBaseCore<SettingsDialogWritingEMails>
{
    public override Tools.Components Component => Tools.Components.EMAIL_ASSISTANT;
    
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

    protected override ChatThread ConvertToChatThread => (this.chatThread ?? new()) with
    {
        SystemPrompt = SystemPrompts.DEFAULT,
    };
    
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
        await this.form!.Validate();
        if (!this.inputIsValid)
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