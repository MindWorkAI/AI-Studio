using System.Text;

using AIStudio.Chat;
using AIStudio.Dialogs;
using AIStudio.Dialogs.Settings;
using AIStudio.Settings.DataModel;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Assistants.DocumentAnalysis;

public partial class DocumentAnalysisAssistant : AssistantBaseCore<SettingsDialogDocumentAnalysis>
{
    [Inject]
    private IDialogService DialogService { get; init; } = null!;
    
    public override Tools.Components Component => Tools.Components.DOCUMENT_ANALYSIS_ASSISTANT;
    
    protected override string Title => T("Document Analysis Assistant");
    
    protected override string Description => T("The document analysis assistant helps you to analyze and extract information from documents based on predefined policies. You can create, edit, and manage document analysis policies that define how documents should be processed and what information should be extracted. Some policies might be protected by your organization and cannot be modified or deleted.");

    protected override string SystemPrompt
    {
        get
        {
            var sb = new StringBuilder();
            
            sb.Append("# Task description");
            sb.AppendLine();

            if (this.loadedDocumentPaths.Count > 1)
            {
                sb.Append($"Your task is to analyse {this.loadedDocumentPaths.Count} documents.");
                sb.Append("Different Documents are divided by a horizontal rule in markdown formatting followed by the name of the document.");
                sb.AppendLine();
            }
            else
            {
                sb.Append("Your task is to analyse a single document.");
                sb.AppendLine();
            }

            var taskDescription = """
                                       The analysis should be done using the policy analysis rules.
                                       The output should be formatted according to the policy output rules. 
                                       The rule sets should be followed strictly. 
                                       Only use information given in the documents or in the policy. 
                                       Never add any information of your own to it.
                                       Keep your answers precise, professional and factual. 
                                       Only answer with the correctly formatted analysis result and do not add any opening or closing remarks.
                                       Answer in the language that is used by the policy or is stated in the output rules.
                                    """;
            
            sb.Append(taskDescription);
            sb.AppendLine();

            sb.Append(this.PromptGetActivePolicy());
            
            return sb.ToString();
        }
    }

    protected override IReadOnlyList<IButtonData> FooterButtons => [];
    
    protected override bool ShowEntireChatThread => true;
    
    protected override bool ShowSendTo => true;

    protected override string SubmitText => T("Analyze documents");

    protected override Func<Task> SubmitAction => this.Analyze;

    protected override bool SubmitDisabled => this.IsNoPolicySelected;

    protected override ChatThread ConvertToChatThread => (this.chatThread ?? new()) with
    {
        SystemPrompt = SystemPrompts.DEFAULT,
    };

    protected override void ResetForm()
    {
        if (!this.MightPreselectValues())
        {
            this.policyName = string.Empty;
            this.policyDescription = string.Empty;
            this.policyIsProtected = false;
            this.policyAnalysisRules = string.Empty;
            this.policyOutputRules = string.Empty;
        }
    }
    
    protected override bool MightPreselectValues()
    {
        if (this.selectedPolicy is not null)
        {
            this.policyName = this.selectedPolicy.PolicyName;
            this.policyDescription = this.selectedPolicy.PolicyDescription;
            this.policyIsProtected = this.selectedPolicy.IsProtected;
            this.policyAnalysisRules = this.selectedPolicy.AnalysisRules;
            this.policyOutputRules = this.selectedPolicy.OutputRules;
            
            return true;
        }
        
        return false;
    }
    
    protected override async Task OnFormChange()
    {
        await this.AutoSave();
    }
    
    #region Overrides of AssistantBase

    protected override async Task OnInitializedAsync()
    {
        this.selectedPolicy = this.SettingsManager.ConfigurationData.DocumentAnalysis.Policies.FirstOrDefault();
        if(this.selectedPolicy is null)
        {
            await this.AddPolicy();
            this.selectedPolicy = this.SettingsManager.ConfigurationData.DocumentAnalysis.Policies.First();
        }
        
        var receivedDeferredContent = MessageBus.INSTANCE.CheckDeferredMessages<string>(Event.SEND_TO_DOCUMENT_ANALYSIS_ASSISTANT).FirstOrDefault();
        if (receivedDeferredContent is not null)
            this.deferredContent = receivedDeferredContent;

        await base.OnInitializedAsync();
    }

    #endregion

    private async Task AutoSave(bool force = false)
    {
        if(this.selectedPolicy is null)
            return;
        
        if(!force && this.selectedPolicy.IsProtected)
            return;
        
        if(!force && this.policyIsProtected)
            return;

        this.selectedPolicy.PreselectedProvider = this.providerSettings.Id;
        
        this.selectedPolicy.PolicyName = this.policyName;
        this.selectedPolicy.PolicyDescription = this.policyDescription;
        this.selectedPolicy.IsProtected = this.policyIsProtected;
        this.selectedPolicy.AnalysisRules = this.policyAnalysisRules;
        this.selectedPolicy.OutputRules = this.policyOutputRules;
        
        await this.SettingsManager.StoreSettings();
    }

    private DataDocumentAnalysisPolicy? selectedPolicy;
    private bool policyIsProtected;
    private string policyName = string.Empty;
    private string policyDescription = string.Empty;
    private string policyAnalysisRules = string.Empty;
    private string policyOutputRules = string.Empty;
#warning Use deferred content for document analysis
    private string deferredContent = string.Empty;
    private HashSet<string> loadedDocumentPaths = [];
    
    private bool IsNoPolicySelectedOrProtected => this.selectedPolicy is null || this.selectedPolicy.IsProtected;
    
    private bool IsNoPolicySelected => this.selectedPolicy is null;
    
    private void SelectedPolicyChanged(DataDocumentAnalysisPolicy? policy)
    {
        this.selectedPolicy = policy;
        this.ResetForm();
    }
    
    private async Task AddPolicy()
    {
        this.SettingsManager.ConfigurationData.DocumentAnalysis.Policies.Add(new ()
        {
            PolicyName = string.Format(T("Policy {0}"), DateTimeOffset.UtcNow),
        });
        
        await this.SettingsManager.StoreSettings();
    }

    private async Task RemovePolicy()
    {
        if(this.selectedPolicy is null)
            return;
        
        if(this.selectedPolicy.IsProtected)
            return;
        
        var dialogParameters = new DialogParameters<ConfirmDialog>
        {
            { x => x.Message, string.Format(T("Are you sure you want to delete the document analysis policy '{0}'?"), this.selectedPolicy.PolicyName) },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Delete document analysis policy"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;
        
        this.SettingsManager.ConfigurationData.DocumentAnalysis.Policies.Remove(this.selectedPolicy);
        this.selectedPolicy = null;
        this.ResetForm();
        
        await this.SettingsManager.StoreSettings();
        this.form?.ResetValidation();
    }
    
    /// <summary>
    /// Gets called when the policy name was changed by typing.
    /// </summary>
    /// <remarks>
    /// This method is used to update the policy name in the selected policy.
    /// Otherwise, the users would be confused when they change the name and the changes are not reflected in the UI.
    /// </remarks>
    private void PolicyNameWasChanged()
    {
        if(this.selectedPolicy is null)
            return;
        
        if(this.selectedPolicy.IsProtected)
            return;
        
        this.selectedPolicy.PolicyName = this.policyName;
    }
    
    private async Task PolicyProtectionWasChanged(bool state)
    {
        if(this.selectedPolicy is null)
            return;
        
        this.policyIsProtected = state;
        this.selectedPolicy.IsProtected = state;
        await this.AutoSave(true);
    }
    
    private string? ValidatePolicyName(string name)
    {
        if(string.IsNullOrWhiteSpace(name))
            return T("Please provide a name for your policy. This name will be used to identify the policy in AI Studio.");
        
        if(name.Length is > 60 or < 6)
            return T("The name of your policy must be between 6 and 60 characters long.");
        
        if(this.SettingsManager.ConfigurationData.DocumentAnalysis.Policies.Where(n => n != this.selectedPolicy).Any(n => n.PolicyName == name))
            return T("A policy with this name already exists. Please choose a different name.");
        
        return null;
    }
    
    private string? ValidatePolicyDescription(string description)
    {
        if(string.IsNullOrWhiteSpace(description))
            return T("Please provide a description for your policy. This description will be used to inform users about the purpose of your document analysis policy.");
        
        if(description.Length is < 32 or > 512)
            return T("The description of your policy must be between 32 and 512 characters long.");
        
        return null;
    }
    
    private string? ValidateAnalysisRules(string analysisRules)
    {
        if(string.IsNullOrWhiteSpace(analysisRules))
            return T("Please provide a description of your analysis rules. This rules will be used to instruct the AI on how to analyze the documents.");
        
        return null;
    }
    
    private string? ValidateOutputRules(string outputRules)
    {
        if(string.IsNullOrWhiteSpace(outputRules))
            return T("Please provide a description of your output rules. This rules will be used to instruct the AI on how to format the output of the analysis.");
        
        return null;
    }

    private string PromptGetActivePolicy()
    {
        return $"""
               # Policy
               The policy is defined as follows:
               
               ## Policy name
               {this.policyName}
               
               ## Policy description 
               {this.policyDescription}
               
               ## Policy analysis rules 
               {this.policyAnalysisRules}
               
               ## Policy output rules
               {this.policyOutputRules}
               """;
    }

    private async Task<string> PromptLoadDocumentsContent()
    {
        if (this.loadedDocumentPaths.Count == 0)
            return string.Empty;
        
        var sb = new StringBuilder();
        var count = 1;
        foreach(var documentPath in this.loadedDocumentPaths)
        {
            sb.Append("---");
            sb.AppendLine();
            sb.Append($"Document {count} file path: {documentPath}");
            sb.AppendLine();
            sb.Append($"Document {count} content:");
            sb.AppendLine();
            
            var fileContent = await this.RustService.ReadArbitraryFileData(documentPath, int.MaxValue);
            sb.Append($"""
                       ```
                       {fileContent}
                       ```
                       """);
            sb.AppendLine();
            sb.AppendLine();
            count += 1;
        }
        
        return sb.ToString();
    }

    private async Task Analyze()
    {
        // if (this.IsNoPolicySelectedOrProtected)
        //    return;

        await this.AutoSave();
        await this.form!.Validate();
        if (!this.inputIsValid)
            return;
        
        this.CreateChatThread();
        
        var userRequest = this.AddUserRequest(
            $"""
                {await this.PromptLoadDocumentsContent()}
             """, hideContentFromUser:true);

        await this.AddAIResponseAsync(userRequest);
    }

    private async Task ExportPolicyAsConfiguration()
    {
        return;
        
# warning Implement the export function
        // do not allow the export of a protected policy
        if (this.IsNoPolicySelectedOrProtected)
            return;
        
        await this.AutoSave();
        await this.form!.Validate();
        
    }
}