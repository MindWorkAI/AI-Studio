using AIStudio.Settings;

namespace AIStudio.Dialogs.Settings;

public partial class SettingsDialogGlossaries : SettingsDialogBase
{
    private async Task AddGlossary()
    {
        var dialogParameters = new DialogParameters<GlossaryDialog>
        {
            { x => x.IsEditing, false },
        };

        var dialogReference = await this.DialogService.ShowAsync<GlossaryDialog>(T("Add Glossary"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;

        var addedGlossary = (Glossary)dialogResult.Data!;
        addedGlossary = addedGlossary with { Num = this.SettingsManager.ConfigurationData.NextGlossaryNum++ };

        this.SettingsManager.ConfigurationData.Glossaries.Add(addedGlossary);

        await this.SettingsManager.StoreSettings();
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }

    private async Task EditGlossary(Glossary glossary)
    {
        var dialogParameters = new DialogParameters<GlossaryDialog>
        {
            { x => x.DataNum, glossary.Num },
            { x => x.DataId, glossary.Id },
            { x => x.DataName, glossary.Name },
            { x => x.DataFilePath, glossary.FilePath },
            { x => x.DataPreselectInTranslation, glossary.PreselectInTranslation },
            { x => x.IsEditing, true },
        };

        var dialogReference = await this.DialogService.ShowAsync<GlossaryDialog>(T("Edit Glossary"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;

        var editedGlossary = (Glossary)dialogResult.Data!;
        this.SettingsManager.ConfigurationData.Glossaries[this.SettingsManager.ConfigurationData.Glossaries.IndexOf(glossary)] = editedGlossary;

        await this.SettingsManager.StoreSettings();
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }

    private async Task ViewGlossary(Glossary glossary)
    {
        var dialogParameters = new DialogParameters<GlossaryDialog>
        {
            { x => x.DataNum, glossary.Num },
            { x => x.DataId, glossary.Id },
            { x => x.DataName, glossary.Name },
            { x => x.DataFilePath, glossary.FilePath },
            { x => x.DataPreselectInTranslation, glossary.PreselectInTranslation },
            { x => x.IsEditing, true },
            { x => x.IsReadOnly, true },
        };

        await this.DialogService.ShowAsync<GlossaryDialog>(T("View Glossary"), dialogParameters, DialogOptions.FULLSCREEN);
    }

    private async Task DeleteGlossary(Glossary glossary)
    {
        var dialogParameters = new DialogParameters<ConfirmDialog>
        {
            { x => x.Message, string.Format(T("Are you sure you want to delete the glossary '{0}'? The glossary file stays where it is."), glossary.Name) },
        };

        var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Delete Glossary"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;

        this.SettingsManager.ConfigurationData.Glossaries.Remove(glossary);
        await this.SettingsManager.StoreSettings();

        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }
}