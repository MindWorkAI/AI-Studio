using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace SourceGeneratedMappings;

/// <summary>
/// Turns the code points of the Lucide icon font into icon strings MudBlazor shows as font icons.
/// </summary>
/// <remarks>
/// The input is Lucide/codepoints.json, copied unchanged from the npm package lucide-static and handed
/// to the compiler as an additional file; Lucide/README.md describes the files and how to update them.
/// MudIcon renders every icon string of the form "class/content" as a span with that class and that
/// text. Each icon string is therefore "lucide/" followed by the glyph of its icon, and the CSS class
/// lucide in wwwroot/app.css selects the font. The generator writes two classes: LucideIcons with a
/// constant for every name, and LucideIconRegistry, which finds an icon by its name at runtime.
/// </remarks>
[Generator]
#pragma warning disable RS1036
public sealed class LucideIconGenerator : IIncrementalGenerator
#pragma warning restore RS1036
{
    private const string GENERATED_NAMESPACE = "AIStudio.Tools";
    private const string CODEPOINTS_PATH_SUFFIX = "/Lucide/codepoints.json";
    private const string FONT_ICON_CLASS = "lucide";

    private static readonly Regex ENTRY = new("\"(?<name>[^\"]*)\"\\s*:\\s*(?<codepoint>[0-9]+)", RegexOptions.CultureInvariant);
    private static readonly Regex VALID_NAME = new("^[a-z][a-z0-9]*(-[a-z0-9]+)*$", RegexOptions.CultureInvariant);

    private static readonly DiagnosticDescriptor CODEPOINTS_MISSING = new(
        id: "LUC001",
        title: "The code points of the Lucide font were not found",
        messageFormat: "No additional file ending in '{0}' was handed to the compiler. LucideIcons and LucideIconRegistry were not generated.",
        category: "SourceGeneration",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NO_ICONS_FOUND = new(
        id: "LUC002",
        title: "The code points of the Lucide font hold no icons",
        messageFormat: "The file '{0}' contains no icon. LucideIcons and LucideIconRegistry were not generated.",
        category: "SourceGeneration",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor ICON_SKIPPED = new(
        id: "LUC003",
        title: "A Lucide icon was skipped",
        messageFormat: "The Lucide icon '{0}' with the code point {1} was skipped: {2}",
        category: "SourceGeneration",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var files = context.AdditionalTextsProvider
            .Where(static file => IsCodepointsFile(file.Path))
            .Select(static (file, token) => new CodepointsFile(file.Path, file.GetText(token)?.ToString() ?? string.Empty))
            .Collect();

        context.RegisterSourceOutput(files, Generate);
    }

    private static bool IsCodepointsFile(string path) => path.Replace('\\', '/').EndsWith(CODEPOINTS_PATH_SUFFIX, StringComparison.OrdinalIgnoreCase);

    private static void Generate(SourceProductionContext context, ImmutableArray<CodepointsFile> files)
    {
        if (files.IsDefaultOrEmpty)
        {
            context.ReportDiagnostic(Diagnostic.Create(CODEPOINTS_MISSING, Location.None, CODEPOINTS_PATH_SUFFIX));
            return;
        }

        var file = files[0];
        var icons = new List<IconDefinition>();
        foreach (Match match in ENTRY.Matches(file.Content))
        {
            var name = match.Groups["name"].Value;
            var codepointText = match.Groups["codepoint"].Value;
            if (!VALID_NAME.IsMatch(name))
            {
                context.ReportDiagnostic(Diagnostic.Create(ICON_SKIPPED, Location.None, name, codepointText, "the name has to start with a lower-case letter and may only hold lower-case letters and digits, separated by single hyphens"));
                continue;
            }

            if (!int.TryParse(codepointText, NumberStyles.None, CultureInfo.InvariantCulture, out var codepoint) || !IsPrivateUse(codepoint))
            {
                context.ReportDiagnostic(Diagnostic.Create(ICON_SKIPPED, Location.None, name, codepointText, "the code point lies outside the private use areas of Unicode"));
                continue;
            }

            icons.Add(new IconDefinition(name, codepoint));
        }

        if (icons.Count == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(NO_ICONS_FOUND, Location.None, file.Path));
            return;
        }

        icons.Sort(static (left, right) => string.CompareOrdinal(left.Name, right.Name));
        context.AddSource("LucideIcons.g.cs", SourceText.From(RenderIcons(icons), Encoding.UTF8));
        context.AddSource("LucideIconRegistry.g.cs", SourceText.From(RenderRegistry(icons), Encoding.UTF8));
    }

    /// <summary>
    /// Icon fonts place their glyphs in the private use areas, where no real character can collide with them.
    /// </summary>
    private static bool IsPrivateUse(int codepoint) => codepoint is >= 0xE000 and <= 0xF8FF or >= 0xF0000 and <= 0x10FFFD;

    private static string RenderIcons(IReadOnlyList<IconDefinition> icons)
    {
        var namesByCodepoint = icons.ToLookup(static icon => icon.Codepoint, static icon => icon.Name);
        var builder = new StringBuilder();

        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.Append("namespace ").Append(GENERATED_NAMESPACE).AppendLine(";");
        builder.AppendLine();
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// Every icon of the Lucide font, as an icon string for MudBlazor.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine("/// <remarks>");
        builder.AppendLine("/// Generated by LucideIconGenerator from Lucide/codepoints.json. The app picks its icons in AppIcons,");
        builder.AppendLine("/// which is the only class that takes them from here.");
        builder.AppendLine("/// </remarks>");
        builder.AppendLine("public static class LucideIcons");
        builder.AppendLine("{");

        for (var index = 0; index < icons.Count; index++)
        {
            var icon = icons[index];
            var sameGlyph = namesByCodepoint[icon.Codepoint].Where(name => name != icon.Name).ToList();

            if (index > 0)
                builder.AppendLine();

            builder.Append("    /// <summary>The Lucide icon \"").Append(icon.Name).Append('"');
            if (sameGlyph.Count > 0)
                builder.Append(", the same glyph as \"").Append(string.Join("\", \"", sameGlyph)).Append('"');

            builder.AppendLine(".</summary>");
            builder.Append("    public const string ").Append(icon.ConstantName).Append(" = \"").Append(FONT_ICON_CLASS).Append('/').Append(EscapeCodepoint(icon.Codepoint)).AppendLine("\";");
        }

        builder.AppendLine("}");
        return builder.ToString();
    }

    private static string RenderRegistry(IReadOnlyList<IconDefinition> icons)
    {
        var builder = new StringBuilder();

        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable enable");
        builder.AppendLine("using System;");
        builder.AppendLine("using System.Collections.Generic;");
        builder.AppendLine();
        builder.Append("namespace ").Append(GENERATED_NAMESPACE).AppendLine(";");
        builder.AppendLine();
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// Finds a Lucide icon by the name lucide.dev shows for it, such as \"house\".");
        builder.AppendLine("/// </summary>");
        builder.AppendLine("/// <remarks>");
        builder.AppendLine("/// Generated by LucideIconGenerator from Lucide/codepoints.json, for icons which are named at runtime,");
        builder.AppendLine("/// such as those in assistant plugins. Aliases count as names of their own.");
        builder.AppendLine("/// </remarks>");
        builder.AppendLine("public static class LucideIconRegistry");
        builder.AppendLine("{");
        builder.Append("    private static readonly IReadOnlyDictionary<string, string> ICONS_BY_NAME = new Dictionary<string, string>(")
            .Append(icons.Count.ToString(CultureInfo.InvariantCulture))
            .AppendLine(", StringComparer.Ordinal)");
        builder.AppendLine("    {");

        foreach (var icon in icons)
            builder.Append("        [\"").Append(icon.Name).Append("\"] = LucideIcons.").Append(icon.ConstantName).AppendLine(",");

        builder.AppendLine("    };");
        builder.AppendLine();
        builder.AppendLine("    /// <summary>");
        builder.AppendLine("    /// Looks up an icon by its name, which has to match exactly.");
        builder.AppendLine("    /// </summary>");
        builder.AppendLine("    /// <param name=\"name\">The name of the icon, such as \"house\".</param>");
        builder.AppendLine("    /// <param name=\"icon\">The icon string for MudBlazor, when the name is known.</param>");
        builder.AppendLine("    /// <returns>True when the font has an icon of that name.</returns>");
        builder.AppendLine("    public static bool TryGet(string name, out string icon) => ICONS_BY_NAME.TryGetValue(name, out icon!);");
        builder.AppendLine("}");

        return builder.ToString();
    }

    private static string EscapeCodepoint(int codepoint) => codepoint <= 0xFFFF
        ? "\\u" + codepoint.ToString("X4", CultureInfo.InvariantCulture)
        : "\\U" + codepoint.ToString("X8", CultureInfo.InvariantCulture);

    private sealed class CodepointsFile(string path, string content) : IEquatable<CodepointsFile>
    {
        public string Path { get; } = path;

        public string Content { get; } = content;

        public bool Equals(CodepointsFile? other) => other is not null && this.Path == other.Path && this.Content == other.Content;

        public override bool Equals(object? obj) => this.Equals(obj as CodepointsFile);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(this.Path) ^ StringComparer.Ordinal.GetHashCode(this.Content);
    }

    private sealed class IconDefinition(string name, int codepoint)
    {
        public string Name { get; } = name;

        public int Codepoint { get; } = codepoint;

        public string ConstantName { get; } = name.ToUpperInvariant().Replace('-', '_');
    }
}