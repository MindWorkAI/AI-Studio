using Markdig;
using System.Text;

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

        return RemoveSharedIndentation(value.AsSpan());
    }

    private static string RemoveSharedIndentation(ReadOnlySpan<char> value)
    {
        var firstContentLineStart = -1;
        var lastContentLineStart = -1;
        var lastContentLineEnd = -1;
        var commonIndentation = int.MaxValue;
        var position = 0;

        while (TryGetNextLine(value, position, out var lineStart, out var currentLineEnd, out var nextPosition))
        {
            var lineContent = value[lineStart..currentLineEnd];
            if (IsWhiteSpace(lineContent))
            {
                position = nextPosition;
                continue;
            }

            if (firstContentLineStart < 0)
                firstContentLineStart = lineStart;

            lastContentLineStart = lineStart;
            lastContentLineEnd = currentLineEnd;
            commonIndentation = Math.Min(commonIndentation, CountIndentation(lineContent));
            position = nextPosition;
        }

        if (firstContentLineStart < 0)
            return string.Empty;

        if (commonIndentation == int.MaxValue)
            commonIndentation = 0;

        var builder = new StringBuilder(lastContentLineEnd - firstContentLineStart);
        var shouldAppendLineBreak = false;
        position = firstContentLineStart;
        
        while (TryGetNextLine(value, position, out var lineStart, out var lineEnd, out var nextPosition))
        {
            var lineContent = value[lineStart..lineEnd];

            if (shouldAppendLineBreak)
                builder.Append('\n');

            if (IsWhiteSpace(lineContent))
                shouldAppendLineBreak = true;
            else if (lineContent.Length > commonIndentation)
            {
                builder.Append(lineContent[commonIndentation..]);
                shouldAppendLineBreak = true;
            }
            else
                shouldAppendLineBreak = true;

            if (lineStart == lastContentLineStart)
                break;

            position = nextPosition;
        }

        return builder.ToString();
    }

    private static bool IsWhiteSpace(ReadOnlySpan<char> value)
    {
        foreach (var character in value)
        {
            if (!char.IsWhiteSpace(character))
                return false;
        }

        return true;
    }

    private static int CountIndentation(ReadOnlySpan<char> value)
    {
        var indentation = 0;
        while (indentation < value.Length && char.IsWhiteSpace(value[indentation]))
            indentation++;

        return indentation;
    }

    private static bool TryGetNextLine(ReadOnlySpan<char> value, int position, out int lineStart, out int lineEnd, out int nextPosition)
    {
        if (position > value.Length)
        {
            lineStart = 0;
            lineEnd = 0;
            nextPosition = position;
            return false;
        }

        lineStart = position;
        for (var i = position; i < value.Length; i++)
        {
            if (value[i] != '\n')
                continue;

            lineEnd = i > lineStart && value[i - 1] == '\r'
                ? i - 1
                : i;

            nextPosition = i + 1;
            return true;
        }

        lineEnd = value.Length;
        nextPosition = value.Length + 1;
        return true;
    }
}