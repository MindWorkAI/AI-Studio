using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Rust;

namespace AIStudio.Components.Settings;

public partial class SettingsPanelApp : SettingsPanelBase
{
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
        await this.RustService.CopyText2Clipboard(this.Snackbar, secret);
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
        foreach (var provider in this.SettingsManager.ConfigurationData.TranscriptionProviders)
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
        if (ManagedConfiguration.TryGet(x => x.App, x => x.EnabledPreviewFeatures, out var meta) && meta.HasPluginContribution)
            return meta.PluginContribution.Where(x => !x.IsReleased()).ToHashSet();

        return [];
    }

    private bool IsPluginContributedPreviewFeature(PreviewFeatures feature)
    {
        if (feature.IsReleased())
            return false;

        if (!ManagedConfiguration.TryGet(x => x.App, x => x.EnabledPreviewFeatures, out var meta) || !meta.HasPluginContribution)
            return false;

        return meta.PluginContribution.Contains(feature);
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