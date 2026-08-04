using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

public sealed class UpdatePolicy(SettingsManager settingsManager, RuntimeInfoResponse runtimeInfo)
{
    public UpdatePolicyMode CurrentMode => settingsManager.ConfigurationData.App.UpdateInterval is UpdateInterval.DISABLE_UPDATES
        ? UpdatePolicyMode.ENTERPRISE_DISABLED
        : runtimeInfo.LinuxPackageType switch
        {
            "flatpak" => UpdatePolicyMode.FLATPAK,
            _ => UpdatePolicyMode.SELF_UPDATE
        };

    public bool AllowsManualChecks => this.CurrentMode is UpdatePolicyMode.SELF_UPDATE;

    public bool AllowsAutomaticChecks => this.CurrentMode is UpdatePolicyMode.SELF_UPDATE &&
        settingsManager.ConfigurationData.App.UpdateInterval is not UpdateInterval.NO_CHECK;

    public bool AllowsInstallations => this.CurrentMode is UpdatePolicyMode.SELF_UPDATE;
}