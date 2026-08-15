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
            _ => runtimeInfo.InstallationKind switch
            {
                //
                // The runtime already refuses to update these installations. We mirror its decision
                // here so that the UI explains the situation instead of offering update actions that
                // would silently do nothing:
                //
                InstallationKind.MANAGED => UpdatePolicyMode.MANAGED_INSTALLATION,
                InstallationKind.UNSUPPORTED_LOCATION => UpdatePolicyMode.UNSUPPORTED_INSTALLATION_LOCATION,
                _ => UpdatePolicyMode.SELF_UPDATE
            }
        };

    public bool AllowsManualChecks => this.CurrentMode is UpdatePolicyMode.SELF_UPDATE;

    public bool AllowsAutomaticChecks => this.CurrentMode is UpdatePolicyMode.SELF_UPDATE &&
        settingsManager.ConfigurationData.App.UpdateInterval is not UpdateInterval.NO_CHECK;

    public bool AllowsInstallations => this.CurrentMode is UpdatePolicyMode.SELF_UPDATE;
}