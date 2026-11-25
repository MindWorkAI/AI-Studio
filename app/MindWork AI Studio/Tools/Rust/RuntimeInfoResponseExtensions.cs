namespace AIStudio.Tools.Rust;

public static class RuntimeInfoResponseExtensions
{
    /// <param name="runtimeInfo">What the runtime tells about itself.</param>
    extension(RuntimeInfoResponse runtimeInfo)
    {
        /// <summary>
        /// Whether a document can be opened at a page at all.
        /// </summary>
        /// <remarks>
        /// Never inside a Flatpak: its sandbox lets AI Studio start no program itself, and the desktop
        /// portal, which opens the document instead, knows no pages, see PageOutcome in
        /// runtime/src/file_actions.rs. Everywhere else, whether the page arrives depends on the
        /// program, which the runtime learns only while it opens the document.
        /// </remarks>
        /// <value>False inside a Flatpak.</value>
        public bool CanOpenDocumentsAtPage => runtimeInfo.LinuxPackageType is not LinuxPackageType.FLATPAK;
    }
}