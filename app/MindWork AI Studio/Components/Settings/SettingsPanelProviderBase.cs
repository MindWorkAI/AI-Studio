using AIStudio.Dialogs;
using AIStudio.Tools.PluginSystem;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Components.Settings;

public abstract class SettingsPanelProviderBase : SettingsPanelBase
{
    /// <summary>
    /// Exports the provider configuration as Lua code, optionally including the encrypted API key if the provider has one
    /// configured and the user agrees to include it. The exportFunc should generate the Lua code based on the provided
    /// encrypted API key (which may be null if the user chose not to include it or if encryption is not available).
    /// The generated Lua code is then copied to the clipboard for easy sharing.
    /// </summary>
    /// <param name="secretId">The secret ID of the provider to check for an API key.</param>
    /// <param name="storeType">The type of secret store to check for the API key (e.g., LLM provider, transcription provider, etc.).</param>
    /// <param name="exportFunc">The function that generates the Lua code for the provider configuration, given the optional encrypted API key.</param>
    protected async Task ExportProvider(ISecretId secretId, SecretStoreType storeType, Func<string?, string> exportFunc)
    {
        string? encryptedApiKey = null;

        // Check if the provider has an API key stored:
        var apiKeyResponse = await this.RustService.GetAPIKey(secretId, storeType, isTrying: true);
        if (apiKeyResponse.Success)
        {
            // Ask the user if they want to export the API key:
            var dialogParameters = new DialogParameters<ConfirmDialog>
            {
                { x => x.Message, T("This provider has an API key configured. Do you want to include the encrypted API key in the export? Note: The recipient will need the same encryption secret to use the API key.") },
            };

            var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Export API Key?"), dialogParameters, DialogOptions.FULLSCREEN);
            var dialogResult = await dialogReference.Result;
            if (dialogResult is { Canceled: false })
            {
                // User wants to export the API key - encrypt it:
                var encryption = PluginFactory.EnterpriseEncryption;
                if (encryption?.IsAvailable == true)
                {
                    var decryptedApiKey = await apiKeyResponse.Secret.Decrypt(Program.ENCRYPTION);
                    if (encryption.TryEncrypt(decryptedApiKey, out var encrypted))
                        encryptedApiKey = encrypted;
                }
                else
                {
                    // No encryption secret available - inform the user:
                    this.Snackbar.Add(T("Cannot export the encrypted API key: No enterprise encryption secret is configured."), Severity.Warning);
                }
            }
        }

        var luaCode = exportFunc(encryptedApiKey);
        if (string.IsNullOrWhiteSpace(luaCode))
            return;

        await this.RustService.CopyText2Clipboard(this.Snackbar, luaCode);
    }
}
