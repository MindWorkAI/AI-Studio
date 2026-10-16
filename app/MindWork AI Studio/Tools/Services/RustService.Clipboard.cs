using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

public sealed partial class RustService
{
    /// <summary>
    /// Tries to copy the given text to the clipboard.
    /// </summary>
    /// <remarks>
    /// The outcome is reported through the message bus. Callers used to hand in their snackbar, which
    /// was the reason most components injected one at all, and it meant this notification was styled
    /// here instead of together with every other notification of the app.
    /// </remarks>
    /// <param name="text">The text to copy to the clipboard.</param>
    public async Task CopyText2Clipboard(string text)
    {
        var message = TB("Successfully copied the text to your clipboard");
        var succeeded = false;
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

            succeeded = true;
            this.logger!.LogDebug("Successfully copied the text to the clipboard.");
        }
        finally
        {
            if (succeeded)
                await MessageBus.INSTANCE.SendSuccess(new(Icons.Material.Filled.ContentCopy, message));
            else
                await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.ContentCopy, message));
        }
    }
}