using AIStudio.Settings;

namespace AIStudio.Dialogs.Settings;

public partial class SettingsDialogProfiles : SettingsDialogBase
{
    private async Task AddProfile()
    {
        var dialogParameters = new DialogParameters<ProfileDialog>
        {
            { x => x.IsEditing, false },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<ProfileDialog>(T("Add Profile"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;
        
        var addedProfile = (Profile)dialogResult.Data!;
        addedProfile = addedProfile with { Num = this.SettingsManager.ConfigurationData.NextProfileNum++ };
        
        this.SettingsManager.ConfigurationData.Profiles.Add(addedProfile);
        
        await this.SettingsManager.StoreSettings();
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }
    
    private async Task EditProfile(Profile profile)
    {
        var dialogParameters = new DialogParameters<ProfileDialog>
        {
            { x => x.DataNum, profile.Num },
            { x => x.DataId, profile.Id },
            { x => x.DataName, profile.Name },
            { x => x.DataNeedToKnow, profile.NeedToKnow },
            { x => x.DataActions, profile.Actions },
            { x => x.IsEditing, true },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<ProfileDialog>(T("Edit Profile"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;
        
        var editedProfile = (Profile)dialogResult.Data!;
        this.SettingsManager.ConfigurationData.Profiles[this.SettingsManager.ConfigurationData.Profiles.IndexOf(profile)] = editedProfile;
        
        await this.SettingsManager.StoreSettings();
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }

    private async Task DeleteProfile(Profile profile)
    {
        var dialogParameters = new DialogParameters
        {
            { "Message", string.Format(T("Are you sure you want to delete the profile '{0}'?"), profile.Name) },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Delete Profile"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;
        
        this.SettingsManager.ConfigurationData.Profiles.Remove(profile);
        await this.SettingsManager.StoreSettings();
        
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }
}