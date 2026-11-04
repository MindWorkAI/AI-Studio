using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Rust;

namespace AIStudio.Tools;

/// <summary>
/// What the export menu and the selections of format templates share: which templates they list,
/// and how they show each one.
/// </summary>
public static class FormatTemplateExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(FormatTemplateExtensions).Namespace, nameof(FormatTemplateExtensions));

    /// <param name="formatTemplates">The format templates, e.g., those of the settings.</param>
    extension(IEnumerable<FormatTemplate> formatTemplates)
    {
        /// <summary>
        /// Returns the format templates of the given kind, in the order the settings list them.
        /// </summary>
        /// <param name="kind">The kind of template, one of the children of FileTypes.FORMAT_TEMPLATE.</param>
        /// <returns>The format templates whose file is of that kind.</returns>
        public IEnumerable<FormatTemplate> Serving(FileTypeFilter kind) => formatTemplates.Where(formatTemplate => formatTemplate.Serves(kind));
    }

    /// <param name="formatTemplate">The format template.</param>
    extension(FormatTemplate formatTemplate)
    {
        /// <summary>
        /// The icon of the format template: the one of the organization for a template it manages.
        /// </summary>
        /// <remarks>
        /// An extension rather than a property of the record, which would end up in the stored settings.
        /// </remarks>
        public string Icon => formatTemplate.IsEnterpriseConfiguration ? Icons.Material.Filled.Business : Icons.Material.Filled.Description;

        /// <summary>
        /// The kind of the template and the extension of its file, as the list of the templates shows
        /// it, e.g., "PowerPoint template (.potx)".
        /// </summary>
        /// <remarks>
        /// A new kind of template, which joins FileTypes.FORMAT_TEMPLATE, needs a name here as well.
        /// The names are translated here rather than taken from the file types, whose names are fixed
        /// in the language AI Studio started with.
        /// </remarks>
        public string TypeName
        {
            get
            {
                var kind = formatTemplate.Serves(FileTypes.MS_WORD_TEMPLATE) ? TB("Word template")
                    : formatTemplate.Serves(FileTypes.POWER_POINT_TEMPLATE) ? TB("PowerPoint template")
                    : TB("Unknown template");

                var extension = Path.GetExtension(formatTemplate.FilePath).ToLowerInvariant();
                return extension.Length > 0 ? $"{kind} ({extension})" : kind;
            }
        }
    }
}