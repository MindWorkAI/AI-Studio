using DocumentFormat.OpenXml.Packaging;

using P = DocumentFormat.OpenXml.Presentation;

namespace AIStudio.Assistants.SlideBuilder;

/// <summary>
/// Selects deterministic title and content layouts from a PowerPoint template.
/// </summary>
internal static class PowerPointTemplateAnalyzer
{
    /// <summary>
    /// Validates the template and finds the first best matching layouts in presentation order.
    /// </summary>
    internal static PowerPointTemplateAnalysisResult Analyze(string templatePath)
    {
        if (string.IsNullOrWhiteSpace(templatePath))
            return Failed("Please select a PowerPoint template.");

        if (!File.Exists(templatePath))
            return Failed("The selected PowerPoint template is no longer available.");

        if (!string.Equals(Path.GetExtension(templatePath), ".pptx", StringComparison.OrdinalIgnoreCase))
            return Failed("The template must be a PowerPoint .pptx file.");

        try
        {
            using var document = PresentationDocument.Open(templatePath, false);
            var presentationPart = document.PresentationPart;
            if (presentationPart?.Presentation is null)
                return Failed("The selected file does not contain a PowerPoint presentation.");

            var layouts = EnumerateLayouts(presentationPart).ToList();
            var titleLayout = layouts
                .Select((layout, index) => new LayoutCandidate(layout, index, ScoreTitleLayout(layout)))
                .Where(candidate => candidate.Score > 0)
                .OrderByDescending(candidate => candidate.Score)
                .ThenBy(candidate => candidate.Index)
                .FirstOrDefault();
            if (titleLayout is null)
                return Failed("The PowerPoint template does not contain a title layout with a title placeholder.");

            var contentLayout = layouts
                .Select((layout, index) => new LayoutCandidate(layout, index, ScoreContentLayout(layout)))
                .Where(candidate => candidate.Score > 0)
                .OrderByDescending(candidate => candidate.Score)
                .ThenBy(candidate => candidate.Index)
                .FirstOrDefault();
            if (contentLayout is null)
                return Failed("The PowerPoint template does not contain a layout with title and content placeholders.");

            return new(
                new(
                    titleLayout.Layout.Uri.ToString(),
                    contentLayout.Layout.Uri.ToString()),
                string.Empty);
        }
        catch (OpenXmlPackageException)
        {
            return Failed("The selected file is not a readable PowerPoint presentation.");
        }
        catch (FileFormatException)
        {
            return Failed("The selected file is not a readable PowerPoint presentation.");
        }
        catch (IOException)
        {
            return Failed("The selected PowerPoint template could not be read.");
        }
        catch (UnauthorizedAccessException)
        {
            return Failed("The selected PowerPoint template could not be accessed.");
        }
    }

    private static IEnumerable<SlideLayoutPart> EnumerateLayouts(PresentationPart presentationPart)
    {
        var masterIds = presentationPart.Presentation?.SlideMasterIdList?.Elements<P.SlideMasterId>() ?? [];
        foreach (var masterId in masterIds)
        {
            var relationshipId = masterId.RelationshipId?.Value;
            if (string.IsNullOrEmpty(relationshipId) || presentationPart.GetPartById(relationshipId) is not SlideMasterPart masterPart)
                continue;

            var layoutIds = masterPart.SlideMaster?.SlideLayoutIdList?.Elements<P.SlideLayoutId>() ?? [];
            foreach (var layoutId in layoutIds)
            {
                relationshipId = layoutId.RelationshipId?.Value;
                if (!string.IsNullOrEmpty(relationshipId) && masterPart.GetPartById(relationshipId) is SlideLayoutPart layoutPart)
                    yield return layoutPart;
            }
        }
    }

    private static int ScoreTitleLayout(SlideLayoutPart layout)
    {
        var placeholderTypes = GetPlaceholderTypes(layout);
        var titleScore = placeholderTypes.Contains(P.PlaceholderValues.CenteredTitle)
            ? 20
            : placeholderTypes.Contains(P.PlaceholderValues.Title)
                ? 10
                : 0;
        if (titleScore == 0)
            return 0;

        return titleScore + (placeholderTypes.Contains(P.PlaceholderValues.SubTitle) ? 100 : 0);
    }

    private static int ScoreContentLayout(SlideLayoutPart layout)
    {
        var placeholderTypes = GetPlaceholderTypes(layout);
        if (!placeholderTypes.Contains(P.PlaceholderValues.Title) && !placeholderTypes.Contains(P.PlaceholderValues.CenteredTitle))
            return 0;

        if (placeholderTypes.Contains(P.PlaceholderValues.Body))
            return 20;

        return placeholderTypes.Contains(P.PlaceholderValues.Object) ? 10 : 0;
    }

    private static HashSet<P.PlaceholderValues> GetPlaceholderTypes(SlideLayoutPart layout) => layout
        .SlideLayout?
        .CommonSlideData?
        .ShapeTree?
        .Elements<P.Shape>()
        .Where(shape => shape.TextBody is not null)
        .Select(GetPlaceholderType)
        .Where(type => type is not null)
        .Select(type => type!.Value)
        .ToHashSet() ?? [];

    private static P.PlaceholderValues? GetPlaceholderType(P.Shape shape) => shape
        .NonVisualShapeProperties?
        .ApplicationNonVisualDrawingProperties?
        .GetFirstChild<P.PlaceholderShape>()?
        .Type?
        .Value;

    private static PowerPointTemplateAnalysisResult Failed(string issue) => new(null, issue);

    private sealed record LayoutCandidate(SlideLayoutPart Layout, int Index, int Score);
}
