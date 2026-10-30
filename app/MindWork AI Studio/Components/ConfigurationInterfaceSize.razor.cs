using AIStudio.Settings.DataModel;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// The interface size of the screen AI Studio shows on: a selection of the size, and a slider for the
/// scale of the custom size.
/// </summary>
/// <remarks>
/// Always shows and changes the screen the window is on right now. The runtime reports that screen
/// when the option opens, and again whenever the size on the screen changed, e.g., because the window
/// moved to another screen. The runtime applies the sizes itself; InterfaceSizeService hands them over
/// whenever the settings change.
/// </remarks>
public partial class ConfigurationInterfaceSize : MSGComponentBase
{
    [Inject]
    private RustService RustService { get; init; } = null!;

    /// <summary>
    /// The screen AI Studio shows on, as the runtime reported it last: when the option opened, and
    /// again whenever the size on the screen changed, e.g., because the window moved to another
    /// screen. Null when the runtime could not measure the screen.
    /// </summary>
    private InterfaceSizeResponse? interfaceSizeOnScreen;

    /// <summary>
    /// The screen whose size the option shows and changes, or null when it is unknown.
    /// </summary>
    private InterfaceSizeResponse? MeasuredScreen => this.interfaceSizeOnScreen is { ScreenKey.Length: > 0 } screen ? screen : null;

    /// <summary>
    /// The key of the screen whose size the option shows, or an empty one when the screen is unknown.
    /// </summary>
    private string ScreenKey => this.MeasuredScreen?.ScreenKey ?? string.Empty;

    /// <summary>
    /// Whether this screen offers the app default. It only differs from the system scaling on the
    /// screen of a laptop, which is also the only screen it is the default of.
    /// </summary>
    private bool OffersAppDefault => this.MeasuredScreen?.DefaultSize is InterfaceSize.APP_DEFAULT;

    /// <summary>
    /// The size on this screen, as the selection shows it: the one the user chose, or else the
    /// default size of the screen.
    /// </summary>
    private InterfaceSize SizeOnScreen
    {
        get
        {
            if (this.MeasuredScreen is not { } screen)
                return InterfaceSize.SYSTEM_SCALING;

            var size = this.SettingsManager.ConfigurationData.App.GetInterfaceScreenSize(screen.ScreenKey)?.Size ?? screen.DefaultSize;

            // Where the selection does not offer the app default, the app default shows the system scaling:
            return size is InterfaceSize.APP_DEFAULT && !this.OffersAppDefault ? InterfaceSize.SYSTEM_SCALING : size;
        }
    }

    private string SizeHelp => this.MeasuredScreen switch
    {
        null => T("AI Studio cannot measure this screen right now, so you cannot set the interface size here."),
        { } screen when this.OffersAppDefault => string.Format(I18N.I.Culture, T("Applies to this screen only ({0}). The app default shows AI Studio in the same size on every laptop. System scaling follows your operating system."), this.DescribeScreen(screen)),
        { } screen => string.Format(I18N.I.Culture, T("Applies to this screen only ({0}). System scaling follows your operating system."), this.DescribeScreen(screen)),
    };

    /// <summary>
    /// The custom scale of the given screen, as the slider shows it: the one the user set, or else the
    /// one the screen shows with. Within the range of the screen, as the runtime applies it.
    /// </summary>
    private int ScalePercent(InterfaceSizeResponse screen)
    {
        var percent = this.SettingsManager.ConfigurationData.App.GetInterfaceScreenSize(screen.ScreenKey)?.CustomPercent ?? screen.CurrentPercent;
        return Math.Clamp(percent, screen.MinPercent, screen.MaxPercent);
    }

    /// <summary>
    /// The help of the slider: the range of the given screen, and what its default size comes to for
    /// a comparison, since the slider starts there when the user comes from the default.
    /// </summary>
    private string ScaleHelp(InterfaceSizeResponse screen) => this.OffersAppDefault
        ? string.Format(I18N.I.Culture, T("You can choose from {0} % to {1} %. The app default on this screen is {2} %."), screen.MinPercent, screen.MaxPercent, screen.AppDefaultPercent)
        : string.Format(I18N.I.Culture, T("You can choose from {0} % to {1} %. The system scaling on this screen is {2} %."), screen.MinPercent, screen.MaxPercent, screen.SystemPercent);

    private string DescribeScreen(InterfaceSizeResponse screen) => screen.ScreenDiagonalInches is { } diagonal
        ? string.Format(I18N.I.Culture, T("{0} × {1} pixels, {2:0.#} inches"), screen.ScreenWidth, screen.ScreenHeight, diagonal)
        : string.Format(I18N.I.Culture, T("{0} × {1} pixels"), screen.ScreenWidth, screen.ScreenHeight);

    private void UpdateScalePercent(int percent)
    {
        if (this.MeasuredScreen is { } screen)
            this.SettingsManager.ConfigurationData.App.RememberInterfaceScreenSize(new(screen.ScreenKey, InterfaceSize.CUSTOM, percent));
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
    private async Task UpdateSize(InterfaceSize size)
    {
        this.interfaceSizeOnScreen = await this.RustService.GetInterfaceSize() ?? this.interfaceSizeOnScreen;
        if (this.MeasuredScreen is not { } screen)
            return;

        var app = this.SettingsManager.ConfigurationData.App;
        var customPercent = app.GetInterfaceScreenSize(screen.ScreenKey)?.CustomPercent;
        if (size is InterfaceSize.CUSTOM)
            customPercent ??= Math.Clamp(screen.CurrentPercent, screen.MinPercent, screen.MaxPercent);

        app.RememberInterfaceScreenSize(new(screen.ScreenKey, size, customPercent));
    }

    #region Overrides of MSGComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.ApplyFilters([], [ Event.CONFIGURATION_CHANGED, Event.TAURI_EVENT_RECEIVED ]);
        await base.OnInitializedAsync();
        this.interfaceSizeOnScreen = await this.RustService.GetInterfaceSize();
    }

    protected override async Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        switch (triggeredEvent)
        {
            // Another size may call for the slider, or no longer:
            case Event.CONFIGURATION_CHANGED:
                this.StateHasChanged();
                break;

            //
            // The window moved to another screen, or the size on the screen changed otherwise. The
            // option has to show and change the screen the window is on now:
            //
            case Event.TAURI_EVENT_RECEIVED when data is TauriEvent { EventType: TauriEventType.INTERFACE_SIZE_CHANGED }:
                this.interfaceSizeOnScreen = await this.RustService.GetInterfaceSize() ?? this.interfaceSizeOnScreen;
                this.StateHasChanged();
                break;
        }

        await base.ProcessIncomingMessage(sendingComponent, triggeredEvent, data);
    }

    #endregion
}