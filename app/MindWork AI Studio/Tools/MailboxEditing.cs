using AIStudio.Dialogs;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Services;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Tools;

/// <summary>
/// Opens the settings of a mailbox, and stores what the user changed in them.
/// </summary>
/// <remarks>
/// Kept here rather than in the places which offer it -- the data source table and the background
/// embeddings page, which leads to it from a refused sign-in -- so that saving a mailbox works the
/// same way everywhere: the settings are stored, and the mailbox is synced right after.
/// </remarks>
public static class MailboxEditing
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(MailboxEditing).Namespace, nameof(MailboxEditing));

    /// <summary>
    /// Lets the user edit a mailbox, and stores and syncs it when they save.
    /// </summary>
    /// <remarks>
    /// The caller tells the rest of the app that the configuration changed, as the sender of that message.
    /// </remarks>
    /// <param name="dialogService">The dialog service to show the settings with.</param>
    /// <param name="settingsManager">The settings, which hold the mailbox.</param>
    /// <param name="embeddingService">The service which syncs the mailbox.</param>
    /// <param name="mailboxId">The id of the mailbox.</param>
    /// <returns>True when the user saved the mailbox. False when they cancelled, or the mailbox is gone or managed by the organization.</returns>
    public static async Task<bool> EditAsync(IDialogService dialogService, SettingsManager settingsManager, DataSourceEmbeddingService embeddingService, string mailboxId)
    {
        var mailboxes = settingsManager.ConfigurationData.Mailboxes;
        var position = mailboxes.FindIndex(mailbox => mailbox.Id == mailboxId);
        if (position < 0 || mailboxes[position].IsEnterpriseConfiguration)
            return false;

        var storedMailbox = mailboxes[position];
        var availableEmbeddings = settingsManager.GetAllEmbeddingProviders()
            .Select(provider => new ConfigurationSelectData<string>(provider.Name, provider.Id))
            .ToList();

        var dialogParameters = new DialogParameters<DataSourceMailboxDialog>
        {
            { x => x.IsEditing, true },
            { x => x.DataSource, storedMailbox },
            { x => x.LockSource, await embeddingService.ShouldLockDataSourceOriginAsync(storedMailbox.Id) },
            { x => x.AvailableEmbeddings, availableEmbeddings }
        };

        var dialogReference = await dialogService.ShowAsync<DataSourceMailboxDialog>(TB("Edit Mailbox"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return false;

        // Found anew, since the list may have changed while the dialog was open:
        position = mailboxes.IndexOf(storedMailbox);
        if (position < 0)
            return false;

        var editedMailbox = (DataSourceMailbox)dialogResult.Data!;
        mailboxes[position] = editedMailbox;

        await settingsManager.StoreSettings();
        await embeddingService.QueueDataSourceAsync(editedMailbox);
        return true;
    }
}