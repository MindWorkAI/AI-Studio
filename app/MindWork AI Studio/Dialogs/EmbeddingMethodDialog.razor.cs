using AIStudio.Assistants.ERI;
using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

public partial class EmbeddingMethodDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;
    
    /// <summary>
    /// The user chosen embedding name.
    /// </summary>
    [Parameter]
    public string DataEmbeddingName { get; set; } = string.Empty;

    /// <summary>
    /// The user chosen embedding type.
    /// </summary>
    [Parameter]
    public string DataEmbeddingType { get; set; } = string.Empty;

    /// <summary>
    /// The embedding description.
    /// </summary>
    [Parameter]
    public string DataDescription { get; set; } = string.Empty;

    /// <summary>
    /// When is the embedding used?
    /// </summary>
    [Parameter]
    public string DataUsedWhen { get; set; } = string.Empty;

    /// <summary>
    /// A link to the embedding documentation or the source code. Might be null, which means no link is provided.
    /// </summary>
    [Parameter]
    public string DataLink { get; set; } = string.Empty;

    /// <summary>
    /// The embedding method names that are already used. The user must choose a unique name.
    /// </summary>
    [Parameter]
    public IReadOnlyList<string> UsedEmbeddingMethodNames { get; set; } = new List<string>();
    
    /// <summary>
    /// Should the dialog be in editing mode?
    /// </summary>
    [Parameter]
    public bool IsEditing { get; init; }
    
    private static readonly Dictionary<string, object?> SPELLCHECK_ATTRIBUTES = new();
    
    private bool dataIsValid;
    private string[] dataIssues = [];
    
    // We get the form reference from Blazor code to validate it manually:
    private MudForm form = null!;
    
    private EmbeddingInfo CreateEmbeddingInfo() => new(this.DataEmbeddingType, this.DataEmbeddingName, this.DataDescription, this.DataUsedWhen, this.DataLink);
    
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        // Configure the spellchecking for the instance name input:
        this.SettingsManager.InjectSpellchecking(SPELLCHECK_ATTRIBUTES);
        
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
    
    private string? ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return T("The embedding name must not be empty. Please name the embedding.");
        
        if (name.Length > 26)
            return T("The embedding name must not be longer than 26 characters.");
        
        if (this.UsedEmbeddingMethodNames.Contains(name))
            return string.Format(T("The embedding method name '{0}' is already used. Please choose a unique name."), name);
        
        return null;
    }
    
    private string? ValidateType(string type)
    {
        if (string.IsNullOrWhiteSpace(type))
            return T("The embedding type must not be empty. Please specify the embedding type.");
        
        if (type.Length > 56)
            return T("The embedding type must not be longer than 56 characters.");
        
        return null;
    }
    
    private string? ValidateDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return T("The description must not be empty. Please describe the embedding method.");
        
        return null;
    }
    
    private string? ValidateUsedWhen(string usedWhen)
    {
        if (string.IsNullOrWhiteSpace(usedWhen))
            return T("Please describe when the embedding is used. Might be anytime or when certain keywords are present, etc.");
        
        return null;
    }
    
    private async Task Store()
    {
        await this.form.Validate();
        
        // When the data is not valid, we don't store it:
        if (!this.dataIsValid)
            return;
        
        var embeddingInfo = this.CreateEmbeddingInfo();
        this.MudDialog.Close(DialogResult.Ok(embeddingInfo));
    }
    
    private void Cancel() => this.MudDialog.Cancel();
}