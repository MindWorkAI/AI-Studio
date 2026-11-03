using AIStudio.Settings.DataModel;

namespace AIStudio.Tools.Rust;

/// <summary>
/// Asks the runtime to show AI Studio in the sizes the user chose.
/// </summary>
/// <param name="Screens">What the user chose for the screens they set a size for, each screen once. Any other screen gets its default size.</param>
public sealed record SetInterfaceSizeRequest(List<InterfaceScreenSize> Screens);