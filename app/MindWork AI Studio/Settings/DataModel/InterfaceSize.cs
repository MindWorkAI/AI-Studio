namespace AIStudio.Settings.DataModel;

/// <summary>
/// How large AI Studio shows its interface on a screen.
/// </summary>
public enum InterfaceSize
{
    /// <summary>
    /// The same size on every laptop, whatever the scaling of its system. The default on the screen
    /// of a laptop. Larger screens, such as external monitors, and screens which do not report their
    /// size keep the scaling of the system with it.
    /// </summary>
    APP_DEFAULT,

    /// <summary>
    /// The scaling of the system, unchanged. The default on any screen but the one of a laptop.
    /// </summary>
    SYSTEM_SCALING,

    /// <summary>
    /// A scale of the user's own, see <see cref="InterfaceScreenSize.CustomPercent"/>.
    /// </summary>
    CUSTOM,
}