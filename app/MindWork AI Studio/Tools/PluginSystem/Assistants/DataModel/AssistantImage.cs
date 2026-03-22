namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

internal sealed class AssistantImage : AssistantComponentBase
{
    private const string PLUGIN_SCHEME = "plugin://";

    public override AssistantComponentType Type => AssistantComponentType.IMAGE;
    public override Dictionary<string, object> Props { get; set; } = new();
    public override List<IAssistantComponent> Children { get; set; } = new();

    public string Src
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Src));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Src), value);
    }

    public string Alt
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Alt));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Alt), value);
    }

    public string Caption
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Caption));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Caption), value);
    }

    public string Class
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Class));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Class), value);
    }

    public string Style
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Style));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Style), value);
    }

    public string ResolveSource(string pluginPath)
    {
        if (string.IsNullOrWhiteSpace(this.Src))
            return string.Empty;

        var resolved = this.Src;

        if (resolved.StartsWith(PLUGIN_SCHEME, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(pluginPath))
        {
            var relative = resolved[PLUGIN_SCHEME.Length..]
                .TrimStart('/', '\\')
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);
            var filePath = Path.Join(pluginPath, relative);
            if (!File.Exists(filePath))
                return string.Empty;

            var mime = GetImageMimeType(filePath);
            var data = Convert.ToBase64String(File.ReadAllBytes(filePath));
            return $"data:{mime};base64,{data}";
        }

        if (!Uri.TryCreate(resolved, UriKind.Absolute, out var uri))
            return string.Empty;

        return uri.Scheme is "http" or "https" or "data" ? resolved : string.Empty;
    }

    private static string GetImageMimeType(string path)
    {
        var extension = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        return extension switch
        {
            "svg" => "image/svg+xml",
            "png" => "image/png",
            "jpg" => "image/jpeg",
            "jpeg" => "image/jpeg",
            "gif" => "image/gif",
            "webp" => "image/webp",
            "bmp" => "image/bmp",
            _ => "image/png",
        };
    }
}
