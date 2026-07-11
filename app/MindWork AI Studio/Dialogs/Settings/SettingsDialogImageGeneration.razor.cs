using AIStudio.Settings;
using AIStudio.Tools.MCPClient;

namespace AIStudio.Dialogs.Settings;

public partial class SettingsDialogImageGeneration : SettingsDialogBase
{
    private readonly List<ConfigurationSelectData<string>> discoveredTools = [];
    private bool isTestingConnection;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        // The dropdown's item list is only in-memory UI state that starts empty every time
        // this dialog is opened. Seed it with the previously saved tool name so the select
        // shows it right away, without requiring the user to test the connection again.
        var savedToolName = this.SettingsManager.ConfigurationData.ImageGeneration.MCPServerToolName;
        if (!string.IsNullOrWhiteSpace(savedToolName))
            this.discoveredTools.Add(new(savedToolName, savedToolName));
    }

    private async Task OnBearerTokenChanged(string updatedToken)
    {
        this.SettingsManager.ConfigurationData.ImageGeneration.MCPServerBearerToken = updatedToken;
        await this.SettingsManager.StoreSettings();
    }

    private async Task TestConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(this.SettingsManager.ConfigurationData.ImageGeneration.MCPServerUrl))
        {
            this.Snackbar.Add(T("Please provide the URL of the MCP server first."), Severity.Warning);
            return;
        }

        this.isTestingConnection = true;
        try
        {
            var result = await MCPImageToolClient.ListToolsAsync(
                this.SettingsManager.ConfigurationData.ImageGeneration.MCPServerUrl,
                this.SettingsManager.ConfigurationData.ImageGeneration.MCPServerBearerToken);

            if (!result.Successful || result.Data is null)
            {
                this.Snackbar.Add(result.Message, Severity.Error);
                return;
            }

            this.discoveredTools.Clear();
            foreach (var tool in result.Data)
                this.discoveredTools.Add(new(tool.Name, tool.Name));

            if (this.discoveredTools.Count == 0)
            {
                this.Snackbar.Add(T("The MCP server did not report any tools."), Severity.Warning);
                return;
            }

            if (this.discoveredTools.All(tool => tool.Value != this.SettingsManager.ConfigurationData.ImageGeneration.MCPServerToolName))
                this.SettingsManager.ConfigurationData.ImageGeneration.MCPServerToolName = this.discoveredTools[0].Value;

            await this.SettingsManager.StoreSettings();
            this.Snackbar.Add(T("Successfully connected to the MCP server."), Severity.Success);
        }
        finally
        {
            this.isTestingConnection = false;
        }
    }
}
