using AIStudio.Settings;
using AIStudio.Tools.Rust;

namespace AIStudio.Tools;

/// <summary>
/// What the export menu and the selections of format templates share: which templates they list,
/// and how they show each one.
/// </summary>
public static class FormatTemplateExtensions
{
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
    }
}