using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components.Settings;

public partial class SettingsPanelApp : SettingsPanelBase
{
    [Inject]
    private UpdatePolicy UpdatePolicy { get; init; } = null!;

    private UpdatePolicyMode updatePolicyMode;

    private bool CannotUpdateItself => this.updatePolicyMode is UpdatePolicyMode.FLATPAK or UpdatePolicyMode.MANAGED_INSTALLATION or UpdatePolicyMode.UNSUPPORTED_INSTALLATION_LOCATION or UpdatePolicyMode.DEVELOPMENT;

    private UpdateInterval DisplayedUpdateInterval => this.CannotUpdateItself
        ? UpdateInterval.NO_CHECK
        : this.SettingsManager.ConfigurationData.App.UpdateInterval;

    private UpdateInstallation DisplayedUpdateInstallation => this.CannotUpdateItself
        ? UpdateInstallation.MANUAL
        : this.SettingsManager.ConfigurationData.App.UpdateInstallation;

    private string UpdateIntervalHelp => this.updatePolicyMode switch
    {
        UpdatePolicyMode.ENTERPRISE_DISABLED => T("Your organization has disabled update checks and installations."),
        UpdatePolicyMode.FLATPAK => T("AI Studio cannot check for updates when running as a Flatpak. Updates are managed outside the app."),
        UpdatePolicyMode.MANAGED_INSTALLATION => T("This installation does not check for updates itself. Contact the person or organization that installed AI Studio for update information."),
        UpdatePolicyMode.UNSUPPORTED_INSTALLATION_LOCATION => T("AI Studio cannot update itself from its current location, so it does not check for updates."),
        UpdatePolicyMode.DEVELOPMENT => T("Development builds do not check for updates."),
        _ => T("How often should we check for app updates?")
    };

    private string UpdateInstallationHelp => this.updatePolicyMode switch
    {
        UpdatePolicyMode.ENTERPRISE_DISABLED => T("This setting has no effect while updates are disabled by your organization."),
        UpdatePolicyMode.FLATPAK => T("AI Studio cannot install updates when running as a Flatpak. Update it using the Flatpak source or bundle from which you installed it."),
        UpdatePolicyMode.MANAGED_INSTALLATION => T("AI Studio cannot install updates into this installation. Contact the person or organization that installed it for new versions."),
        UpdatePolicyMode.UNSUPPORTED_INSTALLATION_LOCATION => T("AI Studio cannot install updates into its current installation location. Install new versions yourself."),
        UpdatePolicyMode.DEVELOPMENT => T("Development builds do not install updates."),
        _ => T("Should updates be installed automatically or manually?")
    };

    private bool IsUpdateIntervalLocked() => this.updatePolicyMode is UpdatePolicyMode.ENTERPRISE_DISABLED || this.CannotUpdateItself ||
        ManagedConfiguration.TryGet(x => x.App, x => x.UpdateInterval, out var meta) && meta.IsLocked;

    private bool IsUpdateInstallationLocked() => this.updatePolicyMode is UpdatePolicyMode.ENTERPRISE_DISABLED || this.CannotUpdateItself ||
        ManagedConfiguration.TryGet(x => x.App, x => x.UpdateInstallation, out var meta) && meta.IsLocked;

    protected override async Task OnInitializedAsync()
    {
        this.ApplyFilters([], [ Event.CONFIGURATION_CHANGED, Event.GLOBAL_SHORTCUT_CHANGED ]);
        await base.OnInitializedAsync();
        this.updatePolicyMode = this.UpdatePolicy.CurrentMode;
    }

    protected override async Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        if (triggeredEvent is Event.CONFIGURATION_CHANGED)
            this.updatePolicyMode = this.UpdatePolicy.CurrentMode;

        if (triggeredEvent is Event.GLOBAL_SHORTCUT_CHANGED)
            this.StateHasChanged();

        await base.ProcessIncomingMessage(sendingComponent, triggeredEvent, data);
    }

    private ConfigurationShortcutData VoiceRecordingShortcut => new()
    {
        Id = Shortcut.VOICE_RECORDING_TOGGLE,
        Value = () => this.SettingsManager.ConfigurationData.App.ShortcutVoiceRecording,
        ValueUpdate = shortcut => this.SettingsManager.ConfigurationData.App.ShortcutVoiceRecording = shortcut,
        DisplayName = () => this.SettingsManager.ConfigurationData.App.ShortcutVoiceRecordingDisplayName,
        DisplaySource = () => this.SettingsManager.ConfigurationData.App.ShortcutVoiceRecordingDisplaySource,
        DisplayUpdate = this.UpdateShortcutVoiceRecordingDisplay,
    };

    private async Task GenerateEncryptionSecret()
    {
        var secret = EnterpriseEncryption.GenerateSecret();
        await this.RustService.CopyText2Clipboard(secret);
    }
    
    private string GetStartPageHelpText()
    {
        var helpText = T("Choose which page AI Studio should open first when you start the app. Changes take effect the next time you launch AI Studio.");
        if (!ManagedConfiguration.TryGet(x => x.App, x => x.StartPage, out var meta) || meta.ManagedMode is not ManagedConfigurationMode.EDITABLE_DEFAULT)
            return helpText;

        return $"{helpText} {T("Your organization provided a default start page, but you can still change it.")}";
    }

    private IEnumerable<ConfigurationSelectData<string>> GetFilteredTranscriptionProviders()
    {
        yield return new(T("Disable dictation and transcription"), string.Empty);

        var minimumLevel = this.SettingsManager.GetMinimumConfidenceLevel(Tools.Components.APP_SETTINGS);
        foreach (var provider in this.SettingsManager.GetAllTranscriptionProviders())
        {
            if (provider.UsedLLMProvider.GetConfidence(this.SettingsManager).Level >= minimumLevel)
                yield return new(provider.Name, provider.Id);
        }
    }

    private void UpdatePreviewFeatures(PreviewVisibility previewVisibility)
    {
        this.SettingsManager.ConfigurationData.App.PreviewVisibility = previewVisibility;
        var filtered = previewVisibility.FilterPreviewFeatures(this.SettingsManager.ConfigurationData.App.EnabledPreviewFeatures);
        filtered.UnionWith(this.GetPluginContributedPreviewFeatures());
        this.SettingsManager.ConfigurationData.App.EnabledPreviewFeatures = filtered;
    }

    private HashSet<PreviewFeatures> GetPluginContributedPreviewFeatures()
    {
        // Several configuration plugins may contribute at the same time, e.g. one preview feature
        // for the whole organization and another one for a single department:
        if (ManagedConfiguration.TryGet(x => x.App, x => x.EnabledPreviewFeatures, out var meta) && meta.HasPluginContribution)
            return meta.PluginContributions.Values.SelectMany(contribution => contribution).Where(x => !x.IsReleased()).ToHashSet();

        return [];
    }

    private bool IsPluginContributedPreviewFeature(PreviewFeatures feature)
    {
        if (feature.IsReleased())
            return false;

        if (!ManagedConfiguration.TryGet(x => x.App, x => x.EnabledPreviewFeatures, out var meta) || !meta.HasPluginContribution)
            return false;

        return meta.PluginContributions.Values.Any(contribution => contribution.Contains(feature));
    }

    private HashSet<PreviewFeatures> GetSelectedPreviewFeatures()
    {
        var enabled = this.SettingsManager.ConfigurationData.App.EnabledPreviewFeatures.Where(x => !x.IsReleased()).ToHashSet();
        enabled.UnionWith(this.GetPluginContributedPreviewFeatures());
        return enabled;
    }

    private string GetExternalHttpCustomRootCertificateAllowedHostsText()
    {
        return string.Join(Environment.NewLine, this.SettingsManager.ConfigurationData.App.ExternalHttpCustomRootCertificateAllowedHosts.Order(StringComparer.OrdinalIgnoreCase));
    }

    private bool AreExternalHttpCustomRootCertificateDetailsDisabled()
    {
        return !this.SettingsManager.ConfigurationData.App.ExternalHttpCustomRootCertificatesEnabled;
    }

    private void UpdateExternalHttpCustomRootCertificateAllowedHosts(string updatedText)
    {
        var patterns = updatedText
            .Split(['\r', '\n', ';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        this.SettingsManager.ConfigurationData.App.ExternalHttpCustomRootCertificateAllowedHosts = patterns;
    }

    private void UpdateEnabledPreviewFeatures(HashSet<PreviewFeatures> selectedFeatures)
    {
        selectedFeatures.UnionWith(this.GetPluginContributedPreviewFeatures());
        this.SettingsManager.ConfigurationData.App.EnabledPreviewFeatures = selectedFeatures;
    }

    private void UpdateShortcutVoiceRecordingDisplay(string displayName, string displaySource)
    {
        this.SettingsManager.ConfigurationData.App.ShortcutVoiceRecordingDisplayName = displayName;
        this.SettingsManager.ConfigurationData.App.ShortcutVoiceRecordingDisplaySource = displaySource;
    }

    private async Task UpdateLangBehaviour(LangBehavior behavior)
    {
        this.SettingsManager.ConfigurationData.App.LanguageBehavior = behavior;
        await this.MessageBus.SendMessage<bool>(this, Event.PLUGINS_RELOADED);
    }

    private async Task UpdateManuallySelectedLanguage(Guid pluginId)
    {
        this.SettingsManager.ConfigurationData.App.LanguagePluginId = pluginId;
        await this.MessageBus.SendMessage<bool>(this, Event.PLUGINS_RELOADED);
    }
}
