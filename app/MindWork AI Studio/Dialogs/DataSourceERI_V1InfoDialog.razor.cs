// ReSharper disable InconsistentNaming

using System.Text;

using AIStudio.Assistants.ERI;
using AIStudio.Components;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.ERIClient;
using AIStudio.Tools.ERIClient.DataModel;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

using RetrievalInfo = AIStudio.Tools.ERIClient.DataModel.RetrievalInfo;

namespace AIStudio.Dialogs;

public partial class DataSourceERI_V1InfoDialog : MSGComponentBase, IAsyncDisposable, ISecretId
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;
    
    [Parameter]
    public DataSourceERI_V1 DataSource { get; set; }
    
    [Inject]
    private RustService RustService { get; init; } = null!;
    
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        this.eriServerTasks.Add(this.GetERIMetadata());
    }

    #endregion
    
    private readonly CancellationTokenSource cts = new();
    private readonly List<Task> eriServerTasks = new();
    private readonly List<string> dataIssues = [];
    
    private string serverDescription = string.Empty;
    private ProviderType securityRequirements = ProviderType.NONE;
    private IReadOnlyList<RetrievalInfo> retrievalInfoformation = [];
    private RetrievalInfo selectedRetrievalInfo;
    
    private bool IsOperationInProgress { get; set; } = true;
    
    private bool IsConnectionEncrypted() => this.DataSource.Hostname.StartsWith("https://", StringComparison.InvariantCultureIgnoreCase);
    
    private string Port => this.DataSource.Port == 0 ? string.Empty : $"{this.DataSource.Port}";

    private string RetrievalName(RetrievalInfo retrievalInfo)
    {
        var hasId = !string.IsNullOrWhiteSpace(retrievalInfo.Id);
        var hasName = !string.IsNullOrWhiteSpace(retrievalInfo.Name);
        
        if (hasId && hasName)
            return $"[{retrievalInfo.Id}] {retrievalInfo.Name}";
        
        if (hasId)
            return string.Format(T("[{0}] Unnamed retrieval process"), retrievalInfo.Id);
        
        return hasName ? retrievalInfo.Name : T("Unnamed retrieval process");
    }
    
    private string RetrievalParameters(RetrievalInfo retrievalInfo)
    {
        var parameters = retrievalInfo.ParametersDescription;
        if (parameters is null || parameters.Count == 0)
            return T("This retrieval process has no parameters.");
        
        var sb = new StringBuilder();
        foreach (var (paramName, description) in parameters)
        {
            sb.Append(T("Parameter: "));
            sb.AppendLine(paramName);
            sb.AppendLine(description);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private async Task GetERIMetadata()
    {
        this.dataIssues.Clear();
        
        try
        {
            this.IsOperationInProgress = true;
            this.StateHasChanged();
            
            using var client = ERIClientFactory.Get(ERIVersion.V1, this.DataSource);
            if(client is null)
            {
                this.dataIssues.Add(T("Failed to connect to the ERI v1 server. The server is not supported."));
                return;
            }
            
            var loginResult = await client.AuthenticateAsync(this.RustService);
            if (!loginResult.Successful)
            {
                this.dataIssues.Add(loginResult.Message);
                return;
            }
            
            var dataSourceInfo = await client.GetDataSourceInfoAsync(this.cts.Token);
            if (!dataSourceInfo.Successful)
            {
                this.dataIssues.Add(dataSourceInfo.Message);
                return;
            }

            this.serverDescription = dataSourceInfo.Data.Description;
            
            var securityRequirementsResult = await client.GetSecurityRequirementsAsync(this.cts.Token);
            if (!securityRequirementsResult.Successful)
            {
                this.dataIssues.Add(securityRequirementsResult.Message);
                return;
            }

            this.securityRequirements = securityRequirementsResult.Data.AllowedProviderType;
            
            var retrievalInfoResult = await client.GetRetrievalInfoAsync(this.cts.Token);
            if (!retrievalInfoResult.Successful)
            {
                this.dataIssues.Add(retrievalInfoResult.Message);
                return;
            }
            
            this.retrievalInfoformation = retrievalInfoResult.Data ?? [];
            this.selectedRetrievalInfo = this.retrievalInfoformation.FirstOrDefault(x => x.Id == this.DataSource.SelectedRetrievalId);
            this.StateHasChanged();
        }
        catch (Exception e)
        {
            this.dataIssues.Add(string.Format(T("Failed to connect to the ERI v1 server. The message was: {0}"), e.Message));
        }
        finally
        {
            this.IsOperationInProgress = false;
            this.StateHasChanged();
        }
    }
    
    private void Close()
    {
        this.cts.Cancel();
        this.MudDialog.Close();
    }
    
    #region Implementation of ISecretId

    public string SecretId => this.DataSource.Id;
    
    public string SecretName => this.DataSource.Name;

    #endregion

    #region Implementation of IDisposable

    public async ValueTask DisposeAsync()
    {
        try
        {
            await this.cts.CancelAsync();
            await Task.WhenAll(this.eriServerTasks);
        
            this.cts.Dispose();
        }
        catch
        {
            // ignored
        }
    }

    #endregion
}