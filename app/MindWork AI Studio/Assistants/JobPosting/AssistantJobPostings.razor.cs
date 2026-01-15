using AIStudio.Chat;
using AIStudio.Dialogs.Settings;

namespace AIStudio.Assistants.JobPosting;

public partial class AssistantJobPostings : AssistantBaseCore<SettingsDialogJobPostings>
{
    public override Tools.Components Component => Tools.Components.JOB_POSTING_ASSISTANT;
    
    protected override string Title => T("Job Posting");
    
    protected override string Description => T("Provide some key points about the job you want to post. The AI will then formulate a suggestion that you can finalize.");
    
    protected override string SystemPrompt => 
        $"""
        You are an experienced specialist in personnel matters. You write job postings.
        You follow the usual guidelines in the country of the posting. You ensure that
        no individuals are discriminated against or excluded by the job posting. You do
        not ask any questions and you do not repeat the task. You provide your response
        in a Markdown format. Missing information necessary for the job posting, you try
        to derive from the other details provided.
        
        Structure of your response:
        ---
        # <TITLE>
        <Description of the job and the company>
        
        # <TASKS>
        <Describe what takes the job entails>
        
        # <QUALIFICATIONS>
        <What qualifications are required>
        
        # <RESPONSIBILITIES>
        <What responsibilities are associated with the job>
        
        <When available, closing details such as the entry date etc.>
        
        ---
        
        You write the job posting in the following language: {this.SystemPromptLanguage()}.
        """;
    
    protected override IReadOnlyList<IButtonData> FooterButtons => [];
    
    protected override string SubmitText => T("Create the job posting");

    protected override Func<Task> SubmitAction => this.CreateJobPosting;

    protected override bool SubmitDisabled => false;

    protected override bool AllowProfiles => false;
    
    protected override ChatThread ConvertToChatThread => (this.chatThread ?? new()) with
    {
        SystemPrompt = SystemPrompts.DEFAULT,
    };

    protected override void ResetForm()
    {
        this.inputEntryDate = string.Empty;
        this.inputValidUntil = string.Empty;
        if (!this.MightPreselectValues())
        {
            this.selectedTargetLanguage = CommonLanguages.AS_IS;
            this.customTargetLanguage = string.Empty;
            this.inputMandatoryInformation = string.Empty;
            this.inputJobDescription = string.Empty;
            this.inputQualifications = string.Empty;
            this.inputResponsibilities = string.Empty;
            this.inputCompanyName = string.Empty;
            this.inputWorkLocation = string.Empty;
            this.inputCountryLegalFramework = string.Empty;
        }
    }
    
    protected override bool MightPreselectValues()
    {
        if (this.SettingsManager.ConfigurationData.JobPostings.PreselectOptions)
        {
            this.inputMandatoryInformation = this.SettingsManager.ConfigurationData.JobPostings.PreselectedMandatoryInformation;
            if(string.IsNullOrWhiteSpace(this.inputJobDescription))
                this.inputJobDescription = this.SettingsManager.ConfigurationData.JobPostings.PreselectedJobDescription;
            this.inputQualifications = this.SettingsManager.ConfigurationData.JobPostings.PreselectedQualifications;
            this.inputResponsibilities = this.SettingsManager.ConfigurationData.JobPostings.PreselectedResponsibilities;
            this.inputCompanyName = this.SettingsManager.ConfigurationData.JobPostings.PreselectedCompanyName;
            this.inputWorkLocation = this.SettingsManager.ConfigurationData.JobPostings.PreselectedWorkLocation;
            this.inputCountryLegalFramework = this.SettingsManager.ConfigurationData.JobPostings.PreselectedCountryLegalFramework;
            this.selectedTargetLanguage = this.SettingsManager.ConfigurationData.JobPostings.PreselectedTargetLanguage;
            this.customTargetLanguage = this.SettingsManager.ConfigurationData.JobPostings.PreselectOtherLanguage;
            return true;
        }
        
        return false;
    }

    private string inputMandatoryInformation = string.Empty;
    private string inputJobDescription = string.Empty;
    private string inputQualifications = string.Empty;
    private string inputResponsibilities = string.Empty;
    private string inputCompanyName = string.Empty;
    private string inputEntryDate = string.Empty;
    private string inputValidUntil = string.Empty;
    private string inputWorkLocation = string.Empty;
    private string inputCountryLegalFramework = string.Empty;
    private CommonLanguages selectedTargetLanguage = CommonLanguages.AS_IS;
    private string customTargetLanguage = string.Empty;
    
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        var deferredContent = MessageBus.INSTANCE.CheckDeferredMessages<string>(Event.SEND_TO_JOB_POSTING_ASSISTANT).FirstOrDefault();
        if (deferredContent is not null)
            this.inputJobDescription = deferredContent;
        
        await base.OnInitializedAsync();
    }

    #endregion
    
    private string? ValidateCustomLanguage(string language)
    {
        if(this.selectedTargetLanguage == CommonLanguages.OTHER && string.IsNullOrWhiteSpace(language))
            return T("Please provide a custom target language.");
        
        return null;
    }
    
    private string? ValidateJobDescription(string jobDescription)
    {
        if(string.IsNullOrWhiteSpace(jobDescription))
            return T("Please provide a job description.");
        
        return null;
    }
    
    private string? ValidateCountryLegalFramework(string countryLegalFramework)
    {
        if(string.IsNullOrWhiteSpace(countryLegalFramework))
            return T("Please provide the country where the job is posted (legal framework).");
        
        return null;
    }
    
    private string SystemPromptLanguage()
    {
        if(this.selectedTargetLanguage is CommonLanguages.AS_IS)
            return "Use the same language as the input";
        
        if(this.selectedTargetLanguage is CommonLanguages.OTHER)
            return this.customTargetLanguage;
        
        return this.selectedTargetLanguage.Name();
    }
    
    private string UserPromptMandatoryInformation()
    {
        if(string.IsNullOrWhiteSpace(this.inputMandatoryInformation))
            return string.Empty;
        
        return $"""
                # Mandatory Information
                {this.inputMandatoryInformation}
                
                """;
    }
    
    private string UserPromptJobDescription()
    {
        if(string.IsNullOrWhiteSpace(this.inputJobDescription))
            return string.Empty;
        
        return $"""
                # Job Description
                {this.inputJobDescription}
                
                """;
    }
    
    private string UserPromptQualifications()
    {
        if(string.IsNullOrWhiteSpace(this.inputQualifications))
            return string.Empty;
        
        return $"""
                # Qualifications
                {this.inputQualifications}
                
                """;
    }
    
    private string UserPromptResponsibilities()
    {
        if(string.IsNullOrWhiteSpace(this.inputResponsibilities))
            return string.Empty;
        
        return $"""
                # Responsibilities
                {this.inputResponsibilities}
                
                """;
    }
    
    private string UserPromptCompanyName()
    {
        if(string.IsNullOrWhiteSpace(this.inputCompanyName))
            return string.Empty;
        
        return $"""
                # Company Name
                {this.inputCompanyName}
                
                """;
    }
    
    private string UserPromptEntryDate()
    {
        if(string.IsNullOrWhiteSpace(this.inputEntryDate))
            return string.Empty;
        
        return $"""
                # Entry Date
                {this.inputEntryDate}
                
                """;
    }
    
    private string UserPromptValidUntil()
    {
        if(string.IsNullOrWhiteSpace(this.inputValidUntil))
            return string.Empty;
        
        return $"""
                # Job Posting Valid Until
                {this.inputValidUntil}
                
                """;
    }
    
    private string UserPromptWorkLocation()
    {
        if(string.IsNullOrWhiteSpace(this.inputWorkLocation))
            return string.Empty;
        
        return $"""
                # Work Location
                {this.inputWorkLocation}
                
                """;
    }
    
    private string UserPromptCountryLegalFramework()
    {
        if(string.IsNullOrWhiteSpace(this.inputCountryLegalFramework))
            return string.Empty;
        
        return $"""
                # Country where the job is posted (legal framework)
                {this.inputCountryLegalFramework}
                
                """;
    }
    
    private async Task CreateJobPosting()
    {
        await this.form!.Validate();
        if (!this.inputIsValid)
            return;
        
        this.CreateChatThread();
        var time = this.AddUserRequest(
            $"""
                {this.UserPromptCompanyName()}
                {this.UserPromptCountryLegalFramework()}
                {this.UserPromptMandatoryInformation()}
                {this.UserPromptJobDescription()}
                {this.UserPromptQualifications()}
                {this.UserPromptResponsibilities()}
                {this.UserPromptWorkLocation()}
                {this.UserPromptEntryDate()}
                {this.UserPromptValidUntil()}
             """);

        await this.AddAIResponseAsync(time);
    }
}