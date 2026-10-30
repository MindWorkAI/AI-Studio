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

    /// <summary>
    /// The screen AI Studio shows on, as the runtime reported it last: when the panel opened, and
    /// again whenever the size on the screen changed, e.g., because the window moved to another
    /// screen. Null when the runtime could not measure the screen.
    /// </summary>
    private InterfaceSizeResponse? interfaceSizeOnScreen;

    /// <summary>
    /// The screen whose size the panel shows and changes, or null when it is unknown.
    /// </summary>
    private InterfaceSizeResponse? MeasuredScreen => this.interfaceSizeOnScreen is { ScreenKey.Length: > 0 } screen ? screen : null;

    /// <summary>
    /// The key of the screen whose size the panel shows, or an empty one when the screen is unknown.
    /// </summary>
    private string InterfaceScreenKey => this.MeasuredScreen?.ScreenKey ?? string.Empty;

    /// <summary>
    /// Whether this screen offers the app default. It only differs from the system scaling on the
    /// screen of a laptop, which is also the only screen it is the default of.
    /// </summary>
    private bool OffersAppDefault => this.MeasuredScreen?.DefaultSize is InterfaceSize.APP_DEFAULT;

    /// <summary>
    /// The size on this screen, as the selection shows it: the one the user chose, or else the
    /// default size of the screen.
    /// </summary>
    private InterfaceSize InterfaceSizeOnScreen
    {
        get
        {
            if (this.MeasuredScreen is not { } screen)
                return InterfaceSize.SYSTEM_SCALING;

            var size = InterfaceSizeService.GetScreenSize(this.SettingsManager.ConfigurationData.App, screen.ScreenKey)?.Size ?? screen.DefaultSize;

            // Where the selection does not offer the app default, the app default shows the system scaling:
            return size is InterfaceSize.APP_DEFAULT && !this.OffersAppDefault ? InterfaceSize.SYSTEM_SCALING : size;
        }
    }

    private string InterfaceSizeHelp => this.MeasuredScreen switch
    {
        null => T("AI Studio cannot measure this screen right now, so you cannot set its size."),
        { } screen when this.OffersAppDefault => string.Format(T("Applies to this screen only ({0}). The app default shows AI Studio in the same size on every laptop, system scaling follows your operating system."), this.DescribeScreen(screen)),
        { } screen => string.Format(T("Applies to this screen only ({0}). System scaling follows your operating system."), this.DescribeScreen(screen)),
    };

    private int InterfaceScaleMinPercent => this.MeasuredScreen?.MinPercent ?? InterfaceSizeService.MIN_CUSTOM_PERCENT;

    private int InterfaceScaleMaxPercent => this.MeasuredScreen?.MaxPercent ?? InterfaceSizeService.MAX_CUSTOM_PERCENT;

    /// <summary>
    /// The custom scale of this screen, as the slider shows it: the one the user set, or else the
    /// one the screen shows with. Within the range of the screen, as the runtime applies it.
    /// </summary>
    private int InterfaceScalePercent
    {
        get
        {
            if (this.MeasuredScreen is not { } screen)
                return 100;

            var percent = InterfaceSizeService.GetScreenSize(this.SettingsManager.ConfigurationData.App, screen.ScreenKey)?.CustomPercent ?? screen.CurrentPercent;
            return Math.Clamp(percent, screen.MinPercent, screen.MaxPercent);
        }
    }

    /// <summary>
    /// The help of the slider: the range of this screen, and what its default size comes to for a
    /// comparison, since the slider starts there when the user comes from the default.
    /// </summary>
    private string InterfaceScaleHelp => this.MeasuredScreen switch
    {
        null => string.Empty,
        { } screen when this.OffersAppDefault => string.Format(T("You can choose from {0} % to {1} %. The app default on this screen is {2} %."), screen.MinPercent, screen.MaxPercent, screen.AppDefaultPercent),
        { } screen => string.Format(T("You can choose from {0} % to {1} %. The system scaling on this screen is {2} %."), screen.MinPercent, screen.MaxPercent, screen.SystemPercent),
    };

    private string DescribeScreen(InterfaceSizeResponse screen) => screen.ScreenDiagonalInches is { } diagonal
        ? string.Format(T("{0} × {1} pixels, {2:0.#} inches"), screen.ScreenWidth, screen.ScreenHeight, diagonal)
        : string.Format(T("{0} × {1} pixels"), screen.ScreenWidth, screen.ScreenHeight);

    private void UpdateInterfaceScalePercent(int percent)
    {
        if (this.MeasuredScreen is { } screen)
            InterfaceSizeService.RememberScreenSize(this.SettingsManager.ConfigurationData.App, new(screen.ScreenKey, InterfaceSize.CUSTOM, percent));
    }

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
        this.ApplyFilters([], [ Event.CONFIGURATION_CHANGED, Event.GLOBAL_SHORTCUT_CHANGED, Event.TAURI_EVENT_RECEIVED ]);
        await base.OnInitializedAsync();
        this.updatePolicyMode = this.UpdatePolicy.CurrentMode;
        this.interfaceSizeOnScreen = await this.RustService.GetInterfaceSize();
    }

    protected override async Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        if (triggeredEvent is Event.CONFIGURATION_CHANGED)
            this.updatePolicyMode = this.UpdatePolicy.CurrentMode;

        if (triggeredEvent is Event.GLOBAL_SHORTCUT_CHANGED)
            this.StateHasChanged();

        //
        // The window moved to another screen, or the size on the screen changed otherwise. The
        // interface size has to show and change the screen the window is on now:
        //
        if (triggeredEvent is Event.TAURI_EVENT_RECEIVED && data is TauriEvent { EventType: TauriEventType.INTERFACE_SIZE_CHANGED })
        {
            this.interfaceSizeOnScreen = await this.RustService.GetInterfaceSize() ?? this.interfaceSizeOnScreen;
            await this.InvokeAsync(this.StateHasChanged);
        }

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

    private string OpusBitrateHelp => T("Higher bitrates can improve transcription accuracy, especially for quiet or noisy recordings, at the cost of a larger upload to the transcription provider. 128 kbps is recommended.");

    private static bool IsOpusBitrateLocked() => ManagedConfiguration.TryGet(x => x.App, x => x.OpusBitrate, out var meta) && meta.IsLocked;

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
            if (this.SettingsManager.IsProviderUsable(provider.UsedLLMProvider, minimumLevel))
                yield return new(provider.Name, provider.Id);
        }
    }

    private TranscriptionProvider? GetTranscriptionProvider(string providerId)
    {
        var provider = this.SettingsManager.GetTranscriptionProviderById(providerId);
        return provider == TranscriptionProvider.NONE ? null : provider;
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

    /// <summary>
    /// Changes the size on the screen the window shows on.
    /// </summary>
    /// <remarks>
    /// The screen is asked from the runtime right now rather than taken from its last report, in
    /// case the window has only just moved. A screen whose custom scale the user set before goes
    /// back to it with the custom size. Any other starts from the scale AI Studio shows with right
    /// now, so that nothing changes on the screen until the user moves the slider; only a scale
    /// beyond the range of the screen moves to its nearest end.
    /// </remarks>
    private async Task UpdateInterfaceSize(InterfaceSize size)
    {
        this.interfaceSizeOnScreen = await this.RustService.GetInterfaceSize() ?? this.interfaceSizeOnScreen;
        if (this.MeasuredScreen is not { } screen)
            return;

        var app = this.SettingsManager.ConfigurationData.App;
        var customPercent = InterfaceSizeService.GetScreenSize(app, screen.ScreenKey)?.CustomPercent;
        if (size is InterfaceSize.CUSTOM)
            customPercent ??= Math.Clamp(screen.CurrentPercent, screen.MinPercent, screen.MaxPercent);

        InterfaceSizeService.RememberScreenSize(app, new(screen.ScreenKey, size, customPercent));
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
