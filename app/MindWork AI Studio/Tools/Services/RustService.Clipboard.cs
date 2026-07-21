using System.Text.Json;

using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

public sealed partial class RustService
{
    /// <summary>
    /// Tries to copy the given text to the clipboard.
    /// </summary>
    /// <param name="snackbar">The snackbar to show the result.</param>
    /// <param name="text">The text to copy to the clipboard.</param>
    public async Task CopyText2Clipboard(ISnackbar snackbar, string text)
    {
        var message = TB("Successfully copied the text to your clipboard");
        var iconColor = Color.Error;
        var severity = Severity.Error;
        try
        {
            var encryptedText = await text.Encrypt(this.encryptor!);
            var response = await this.http.PostAsync("/clipboard/set", new StringContent(encryptedText.EncryptedData));
            if (!response.IsSuccessStatusCode)
            {
                this.logger!.LogError($"Failed to copy the text to the clipboard due to an network error: '{response.StatusCode}'");
                message = TB("Failed to copy the text to your clipboard.");
                return;
            }

            var state = await response.Content.ReadFromJsonAsync<SetClipboardResponse>(this.jsonRustSerializerOptions);
            if (!state.Success)
            {
                this.logger!.LogError("Failed to copy the text to the clipboard.");
                message = TB("Failed to copy the text to your clipboard.");
                return;
            }
            
            iconColor = Color.Success;
            severity = Severity.Success;
            this.logger!.LogDebug("Successfully copied the text to the clipboard.");
        }
        finally
        {
            snackbar.Add(message, severity, config =>
            {
                config.Icon = Icons.Material.Filled.ContentCopy;
                config.IconSize = Size.Large;
                config.IconColor = iconColor;
            });
        }
    }

    /// <summary>
    /// Tries to copy HTML and its plain-text fallback to the clipboard.
    /// </summary>
    /// <param name="snackbar">The snackbar to show the result.</param>
    /// <param name="plainText">The content used by applications that do not accept HTML.</param>
    /// <param name="htmlText">The content used by applications that accept HTML.</param>
    public async Task CopyRichText2Clipboard(ISnackbar snackbar, string plainText, string htmlText)
    {
        var message = TB("Successfully copied the text to your clipboard");
        var iconColor = Color.Error;
        var severity = Severity.Error;
        try
        {
            var content = JsonSerializer.Serialize(new RichClipboardContent(plainText, htmlText), this.jsonRustSerializerOptions);
            var encryptedText = await content.Encrypt(this.encryptor!);
            var response = await this.http.PostAsync("/clipboard/set-rich-text", new StringContent(encryptedText.EncryptedData));
            if (!response.IsSuccessStatusCode)
            {
                this.logger!.LogError($"Failed to copy rich text to the clipboard due to a network error: '{response.StatusCode}'");
                message = TB("Failed to copy the text to your clipboard.");
                return;
            }

            var state = await response.Content.ReadFromJsonAsync<SetClipboardResponse>(this.jsonRustSerializerOptions);
            if (!state.Success)
            {
                this.logger!.LogError("Failed to copy rich text to the clipboard.");
                message = TB("Failed to copy the text to your clipboard.");
                return;
            }

            iconColor = Color.Success;
            severity = Severity.Success;
            this.logger!.LogDebug("Successfully copied rich text to the clipboard.");
        }
        finally
        {
            snackbar.Add(message, severity, config =>
            {
                config.Icon = Icons.Material.Filled.ContentCopy;
                config.IconSize = Size.Large;
                config.IconColor = iconColor;
            });
        }
    }
}