using AIStudio.Components;
using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

public partial class ProfileDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    /// <summary>
    /// The profile's number in the list.
    /// </summary>
    [Parameter]
    public uint DataNum { get; set; }
    
    /// <summary>
    /// The profile's ID.
    /// </summary>
    [Parameter]
    public string DataId { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// The profile name chosen by the user.
    /// </summary>
    [Parameter]
    public string DataName { get; set; } = string.Empty;
    
    /// <summary>
    /// What should the LLM know about you?
    /// </summary>
    [Parameter]
    public string DataNeedToKnow { get; set; } = string.Empty;

    /// <summary>
    /// What actions should the LLM take?
    /// </summary>
    [Parameter]
    public string DataActions { get; set; } = string.Empty;
    
    /// <summary>
    /// Should the dialog be in editing mode?
    /// </summary>
    [Parameter]
    public bool IsEditing { get; init; }
    
    [Inject]
    private ILogger<ProviderDialog> Logger { get; init; } = null!;
    
    private static readonly Dictionary<string, object?> SPELLCHECK_ATTRIBUTES = new();
    
    /// <summary>
    /// The list of used profile names. We need this to check for uniqueness.
    /// </summary>
    private List<string> UsedNames { get; set; } = [];
    
    private bool dataIsValid;
    private string[] dataIssues = [];
    private string dataEditingPreviousName = string.Empty;
    
    // We get the form reference from Blazor code to validate it manually:
    private MudForm form = null!;

    private Profile CreateProfileSettings() => new()
    {
        Num = this.DataNum,
        Id = this.DataId,
        
        Name = this.DataName,
        NeedToKnow = this.DataNeedToKnow,
        Actions = this.DataActions,
    };

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        // Configure the spellchecking for the instance name input:
        this.SettingsManager.InjectSpellchecking(SPELLCHECK_ATTRIBUTES);
        
        // Load the used instance names:
        this.UsedNames = this.SettingsManager.ConfigurationData.Profiles.Select(x => x.Name.ToLowerInvariant()).ToList();
        
        // When editing, we need to load the data:
        if(this.IsEditing)
        {
            this.dataEditingPreviousName = this.DataName.ToLowerInvariant();
        }
        
        await base.OnInitializedAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Reset the validation when not editing and on the first render.
        // We don't want to show validation errors when the user opens the dialog.
        if(!this.IsEditing && firstRender)
            this.form.ResetValidation();
        
        await base.OnAfterRenderAsync(firstRender);
    }

    #endregion
    
    private async Task Store()
    {
        await this.form.Validate();
        
        // When the data is not valid, we don't store it:
        if (!this.dataIsValid)
            return;
        
        // Use the data model to store the profile.
        // We just return this data to the parent component:
        var addedProfileSettings = this.CreateProfileSettings();
        
        if(this.IsEditing)
            this.Logger.LogInformation($"Edited profile '{addedProfileSettings.Name}'.");
        else
            this.Logger.LogInformation($"Created profile '{addedProfileSettings.Name}'.");
        
        this.MudDialog.Close(DialogResult.Ok(addedProfileSettings));
    }
    
    private string? ValidateNeedToKnow(string text)
    {
        if (string.IsNullOrWhiteSpace(this.DataNeedToKnow) && string.IsNullOrWhiteSpace(this.DataActions))
            return T("Please enter what the LLM should know about you and/or what actions it should take.");
        
        if(text.Length > 444)
            return T("The text must not exceed 444 characters.");
        
        return null;
    }

    private string? ValidateActions(string text)
    {
        if (string.IsNullOrWhiteSpace(this.DataNeedToKnow) && string.IsNullOrWhiteSpace(this.DataActions))
            return T("Please enter what the LLM should know about you and/or what actions it should take.");
        
        if(text.Length > 256)
            return T("The text must not exceed 256 characters.");
        
        return null;
    }

    private string? ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return T("Please enter a profile name.");
        
        if (name.Length > 40)
            return T("The profile name must not exceed 40 characters.");
        
        // The instance name must be unique:
        var lowerName = name.ToLowerInvariant();
        if (lowerName != this.dataEditingPreviousName && this.UsedNames.Contains(lowerName))
            return T("The profile name must be unique; the chosen name is already in use.");
        
        return null;
    }

    private void Cancel() => this.MudDialog.Cancel();
}