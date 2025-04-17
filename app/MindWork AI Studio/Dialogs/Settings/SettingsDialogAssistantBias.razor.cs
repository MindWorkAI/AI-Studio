using AIStudio.Settings.DataModel;

namespace AIStudio.Dialogs.Settings;

public partial class SettingsDialogAssistantBias : SettingsDialogBase
{
    private string StatText => string.Format(T("You have learned about {0} out of {1} biases."), this.SettingsManager.ConfigurationData.BiasOfTheDay.UsedBias.Count, BiasCatalog.ALL_BIAS.Count);
    
    private async Task ResetBiasOfTheDayHistory()
    {
        var dialogParameters = new DialogParameters
        {
            { "Message", T("Are you sure you want to reset your bias-of-the-day statistics? The system will no longer remember which biases you already know. As a result, biases you are already familiar with may be addressed again.") },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Reset your bias-of-the-day statistics"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;
        
        this.SettingsManager.ConfigurationData.BiasOfTheDay.UsedBias.Clear();
        this.SettingsManager.ConfigurationData.BiasOfTheDay.DateLastBiasDrawn = DateOnly.MinValue;
        await this.SettingsManager.StoreSettings();
        
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }

}