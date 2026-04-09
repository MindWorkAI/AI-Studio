using Markdig;

namespace AIStudio.Tools;

public static class Markdown
{
    public static readonly MarkdownPipeline SAFE_MARKDOWN_PIPELINE = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .DisableHtml()
        .Build();

    public static MudMarkdownProps DefaultConfig => new()
    {
        Heading =
        {
            OverrideTypo = typo => typo switch
            {
                Typo.h1 => Typo.h4,
                Typo.h2 => Typo.h5,
                Typo.h3 => Typo.h6,
                Typo.h4 => Typo.h6,
                Typo.h5 => Typo.h6,
                Typo.h6 => Typo.h6,
        
                _ => typo,
            },
        }
    };

    public static string RemoveSharedIndentation(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Replace("\r\n", "\n");
        var lines = normalized.Split('\n');

        var firstContentLine = 0;
        while (firstContentLine < lines.Length && string.IsNullOrWhiteSpace(lines[firstContentLine]))
            firstContentLine++;

        var lastContentLine = lines.Length - 1;
        while (lastContentLine >= firstContentLine && string.IsNullOrWhiteSpace(lines[lastContentLine]))
            lastContentLine--;

        if (firstContentLine > lastContentLine)
            return string.Empty;

        var commonIndentation = int.MaxValue;
        for (var i = firstContentLine; i <= lastContentLine; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var indentation = 0;
            while (indentation < line.Length && char.IsWhiteSpace(line[indentation]))
                indentation++;

            commonIndentation = Math.Min(commonIndentation, indentation);
        }

        if (commonIndentation == int.MaxValue)
            commonIndentation = 0;

        for (var i = firstContentLine; i <= lastContentLine; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                lines[i] = string.Empty;
                continue;
            }

            lines[i] = line.Length <= commonIndentation
                ? string.Empty
                : line[commonIndentation..];
        }

        return string.Join('\n', lines[firstContentLine..(lastContentLine + 1)]);
    }
}