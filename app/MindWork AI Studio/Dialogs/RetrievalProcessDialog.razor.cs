using AIStudio.Assistants.ERI;
using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

public partial class RetrievalProcessDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    /// <summary>
    /// The user chosen retrieval process name.
    /// </summary>
    [Parameter]
    public string DataName { get; set; } = string.Empty;

    /// <summary>
    /// The retrieval process description.
    /// </summary>
    [Parameter]
    public string DataDescription { get; set; } = string.Empty;

    /// <summary>
    /// A link to the retrieval process documentation, paper, Wikipedia article, or the source code.
    /// </summary>
    [Parameter]
    public string DataLink { get; set; } = string.Empty;

    /// <summary>
    /// A dictionary that describes the parameters of the retrieval process. The key is the parameter name,
    /// and the value is a description of the parameter. Although each parameter will be sent as a string,
    /// the description should indicate the expected type and range, e.g., 0.0 to 1.0 for a float parameter.
    /// </summary>
    [Parameter]
    public Dictionary<string, string> DataParametersDescription { get; set; } = new();

    /// <summary>
    /// A list of embeddings used in this retrieval process. It might be empty in case no embedding is used.
    /// </summary>
    [Parameter]
    public HashSet<EmbeddingInfo> DataEmbeddings { get; set; } = new();

    /// <summary>
    /// The available embeddings for the user to choose from.
    /// </summary>
    [Parameter]
    public IReadOnlyList<EmbeddingInfo> AvailableEmbeddings { get; set; } = new List<EmbeddingInfo>();

    /// <summary>
    /// The retrieval process names that are already used. The user must choose a unique name.
    /// </summary>
    [Parameter]
    public IReadOnlyList<string> UsedRetrievalProcessNames { get; set; } = new List<string>();
    
    /// <summary>
    /// Should the dialog be in editing mode?
    /// </summary>
    [Parameter]
    public bool IsEditing { get; init; }
    
    private static readonly Dictionary<string, object?> SPELLCHECK_ATTRIBUTES = new();
    
    private bool dataIsValid;
    private string[] dataIssues = [];
    private List<RetrievalParameter> retrievalParameters = new();
    private RetrievalParameter? selectedParameter;
    private uint nextParameterId = 1;
    
    // We get the form reference from Blazor code to validate it manually:
    private MudForm form = null!;
    
    private RetrievalInfo CreateRetrievalInfo() => new(this.DataName, this.DataDescription, this.DataLink, this.retrievalParameters.ToDictionary(parameter => parameter.Name, parameter => parameter.Description), this.DataEmbeddings.ToList());
    
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        
        // Configure the spellchecking for the instance name input:
        this.SettingsManager.InjectSpellchecking(SPELLCHECK_ATTRIBUTES);
        
        // Convert the parameters:
        this.retrievalParameters = this.DataParametersDescription.Select(pair => new RetrievalParameter { Name = pair.Key, Description = pair.Value }).ToList();
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
            return T("The retrieval process name must not be empty. Please name your retrieval process.");
        
        if (name.Length > 26)
            return T("The retrieval process name must not be longer than 26 characters.");
        
        if (this.UsedRetrievalProcessNames.Contains(name))
            return string.Format(T("The retrieval process name '{0}' must be unique. Please choose a different name."), name);
        
        return null;
    }
    
    private string? ValidateDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return T("The description must not be empty. Please describe the retrieval process.");
        
        return null;
    }
    
    private void AddRetrievalProcessParameter()
    {
        this.retrievalParameters.Add(new() { Name = string.Format(T("New Parameter {0}"), this.nextParameterId++), Description = string.Empty });
    }
    
    private string? ValidateParameterName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return T("The parameter name must not be empty. Please name the parameter.");
        
        if(name.Length > 26)
            return T("The parameter name must not be longer than 26 characters.");
        
        if (this.retrievalParameters.Count(parameter => parameter.Name == name) > 1)
            return string.Format(T("The parameter name '{0}' must be unique. Please choose a different name."), name);
        
        return null;
    }
    
    private string? ValidateParameterDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return string.Format(T("The parameter description must not be empty. Please describe the parameter '{0}'. What data type is it? What is it used for? What are the possible values?"), this.selectedParameter?.Name);
        
        return null;
    }
    
    private string? ValidateParameter(RetrievalParameter parameter)
    {
        if(this.ValidateParameterName(parameter.Name) is { } nameIssue)
            return nameIssue;
        
        if (string.IsNullOrWhiteSpace(parameter.Description))
            return string.Format(T("The parameter description must not be empty. Please describe the parameter '{0}'. What data type is it? What is it used for? What are the possible values?"), parameter.Name);
        
        return null;
    }
    
    private void RemoveRetrievalProcessParameter()
    {
        if (this.selectedParameter is not null)
            this.retrievalParameters.Remove(this.selectedParameter);
        
        this.selectedParameter = null;
    }
    
    private string GetMultiSelectionText(List<EmbeddingInfo> selectedEmbeddings)
    {
        if(selectedEmbeddings.Count == 0)
            return T("No embedding methods selected.");
        
        if(selectedEmbeddings.Count == 1)
            return T("You have selected 1 embedding method.");
        
        return string.Format(T("You have selected {0} embedding methods."), selectedEmbeddings.Count);
    }
    
    private void EmbeddingsChanged(IEnumerable<EmbeddingInfo>? updatedEmbeddings)
    {
        if(updatedEmbeddings is null)
            this.DataEmbeddings = new();
        else
            this.DataEmbeddings = updatedEmbeddings.ToHashSet();
    }
    
    private async Task Store()
    {
        await this.form.Validate();
        foreach (var parameter in this.retrievalParameters)
        {
            if (this.ValidateParameter(parameter) is { } issue)
            {
                this.dataIsValid = false;
                Array.Resize(ref this.dataIssues, this.dataIssues.Length + 1);
                this.dataIssues[^1] = issue;
            }
        }
        
        // When the data is not valid, we don't store it:
        if (!this.dataIsValid || this.dataIssues.Any())
            return;
        
        var retrievalInfo = this.CreateRetrievalInfo();
        this.MudDialog.Close(DialogResult.Ok(retrievalInfo));
    }
    
    private void Cancel() => this.MudDialog.Cancel();
}