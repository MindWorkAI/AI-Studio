using AIStudio.Settings.DataModel;

namespace AIStudio.Tools.Rust;

/// <summary>
/// What the runtime reports about the interface size on the screen AI Studio shows on.
/// </summary>
/// <remarks>
/// All percentages count like the scaling of the operating system: device pixels per pixel of the app.
/// </remarks>
/// <param name="Success">Whether the runtime could measure the screen.</param>
/// <param name="ErrorMessage">What went wrong, when it could not.</param>
/// <param name="Zoom">The zoom of the webview: 1.0 is the scaling of the system.</param>
/// <param name="Size">The size on this screen: the one the user chose for it, or else its default size.</param>
/// <param name="DefaultSize">The size of this screen while the user chose none for it: the app default on the screen of a laptop, the scaling of the system on any other.</param>
/// <param name="ScreenKey">What the size of this screen is stored by: its resolution and its physical size.</param>
/// <param name="ScreenWidth">The width of the screen in device pixels.</param>
/// <param name="ScreenHeight">The height of the screen in device pixels.</param>
/// <param name="ScreenDiagonalInches">The diagonal of the screen in inches, or null when the screen does not report its size.</param>
/// <param name="SystemPercent">The scaling of the system on this screen.</param>
/// <param name="CurrentPercent">The scale AI Studio shows with.</param>
/// <param name="AppDefaultPercent">The scale the app default gives this screen, which is also where its custom scale starts.</param>
/// <param name="MinPercent">The smallest custom scale this screen allows.</param>
/// <param name="MaxPercent">The largest custom scale this screen allows.</param>
public sealed record InterfaceSizeResponse(
    bool Success,
    string ErrorMessage,
    double Zoom,
    InterfaceSize Size,
    InterfaceSize DefaultSize,
    string ScreenKey,
    int ScreenWidth,
    int ScreenHeight,
    double? ScreenDiagonalInches,
    int SystemPercent,
    int CurrentPercent,
    int AppDefaultPercent,
    int MinPercent,
    int MaxPercent);