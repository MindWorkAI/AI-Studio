namespace AIStudio.Settings.DataModel;

/// <summary>
/// The interface size the user chose for one screen.
/// </summary>
/// <param name="ScreenKey">The screen, as the runtime names it: its resolution and its physical size, such as "1920x1200@287x179mm".</param>
/// <param name="Size">The size on this screen.</param>
/// <param name="CustomPercent">The scale of the custom size in percent, counted like the scaling of the operating system, or null when the user never set one for this screen. Kept while another size applies, so that it returns with the custom size.</param>
public sealed record InterfaceScreenSize(string ScreenKey, InterfaceSize Size, int? CustomPercent);