using AIStudio.Dialogs;
using AIStudio.Settings;
using AIStudio.Tools.Glossaries;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Components;

public partial class GlossaryManagement : MSGComponentBase
{
    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    [Inject]
    private GlossaryStore GlossaryStore { get; init; } = null!;

    /// <summary>
    /// The number of entries per glossary; null for a glossary whose entries cannot be read.
    /// </summary>
    private readonly Dictionary<string, int?> entryCounts = new(StringComparer.OrdinalIgnoreCase);

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        foreach (var glossary in this.SettingsManager.ConfigurationData.Glossaries)
            this.entryCounts[glossary.Id] = (await this.GlossaryStore.TryLoadEntriesAsync(glossary.Id))?.Count;
    }

    #endregion

    private string DescribeType(GlossaryType type) => type switch
    {
        GlossaryType.TRANSLATION => T("Translation"),
        GlossaryType.REFERENCE => T("Reference"),

        _ => string.Empty,
    };

    private string DescribeLanguages(Glossary glossary)
    {
        if (glossary.Type is not GlossaryType.TRANSLATION)
            return "—";

        return $"{LanguageName(glossary.SourceLanguage, glossary.CustomSourceLanguage)} → {LanguageName(glossary.TargetLanguage, glossary.CustomTargetLanguage)}";
    }

    private static string LanguageName(CommonLanguages language, string customLanguage) => language is CommonLanguages.OTHER ? customLanguage : language.Name();

    private async Task AddGlossary()
    {
        var dialogParameters = new DialogParameters<GlossaryDialog>
        {
            { x => x.IsEditing, false },
        };

        var dialogReference = await this.DialogService.ShowAsync<GlossaryDialog>(T("Add Glossary"), dialogParameters, DialogOptions.FULLSCREEN_MANUAL_ESCAPE);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled || dialogResult.Data is not GlossaryDialogResult result)
            return;

        // The entries go first: settings which name a glossary without its file would show it empty,
        // while a file without a glossary in the settings harms nobody.
        await this.GlossaryStore.SaveEntriesAsync(result.Glossary.Id, result.Entries);
        var addedGlossary = result.Glossary with { Num = this.SettingsManager.ConfigurationData.NextGlossaryNum++ };
        this.SettingsManager.ConfigurationData.Glossaries.Add(addedGlossary);
        this.entryCounts[addedGlossary.Id] = result.Entries.Count;

        await this.SettingsManager.StoreSettings();
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }

    private async Task EditGlossary(Glossary glossary)
    {
        // Entries which cannot be read must not look like an empty glossary: saving it, even only
        // to rename it, would overwrite them. The user decides whether to start over:
        var entries = await this.GlossaryStore.TryLoadEntriesAsync(glossary.Id);
        if (entries is null)
        {
            var confirmParameters = new DialogParameters<ConfirmDialog>
            {
                { x => x.Message, string.Format(T("AI Studio could not read the entries of the glossary '{0}'. When you continue, the glossary starts without entries, and saving it replaces the stored entries for good. Do you want to continue?"), glossary.Name) },
            };

            var confirmReference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Unreadable Glossary Entries"), confirmParameters, DialogOptions.FULLSCREEN);
            var confirmResult = await confirmReference.Result;
            if (confirmResult is null || confirmResult.Canceled)
                return;

            entries = [];
        }

        var dialogParameters = new DialogParameters<GlossaryDialog>
        {
            { x => x.Glossary, glossary },
            { x => x.Entries, entries },
            { x => x.IsEditing, true },
        };

        var dialogReference = await this.DialogService.ShowAsync<GlossaryDialog>(T("Edit Glossary"), dialogParameters, DialogOptions.FULLSCREEN_MANUAL_ESCAPE);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled || dialogResult.Data is not GlossaryDialogResult result)
            return;

        await this.GlossaryStore.SaveEntriesAsync(result.Glossary.Id, result.Entries);
        var glossaries = this.SettingsManager.ConfigurationData.Glossaries;
        glossaries[glossaries.IndexOf(glossary)] = result.Glossary;
        this.entryCounts[result.Glossary.Id] = result.Entries.Count;

        await this.SettingsManager.StoreSettings();
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }

    private async Task DeleteGlossary(Glossary glossary)
    {
        var dialogParameters = new DialogParameters<ConfirmDialog>
        {
            { x => x.Message, string.Format(T("Are you sure you want to delete the glossary '{0}' with all of its entries?"), glossary.Name) },
        };

        var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Delete Glossary"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;

        this.SettingsManager.ConfigurationData.Glossaries.Remove(glossary);
        await this.SettingsManager.StoreSettings();
        await this.GlossaryStore.DeleteEntriesAsync(glossary.Id);
        this.entryCounts.Remove(glossary.Id);

        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }
}