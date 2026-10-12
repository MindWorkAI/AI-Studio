using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;

using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace AIStudio.Assistants.SlideBuilder;

/// <summary>
/// Replaces a template's slides with native title-and-content slides while preserving its design parts.
/// </summary>
internal static class PowerPointPresentationRenderer
{
    /// <summary>
    /// Creates a validated presentation next to the path selected by the user.
    /// </summary>
    internal static PowerPointRenderResult Render(
        string templatePath,
        string outputPath,
        SlideDeckPlan plan,
        PowerPointTemplateAnalysis analysis)
    {
        outputPath = EnsureExtension(outputPath);
        if (PathsEqual(templatePath, outputPath))
            return Failed("The generated presentation must not overwrite its template.");

        var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (string.IsNullOrEmpty(outputDirectory) || !Directory.Exists(outputDirectory))
            return Failed("The selected output folder is not available.");

        var temporaryPath = Path.Combine(
            outputDirectory,
            $".{Path.GetFileName(outputPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            File.Copy(templatePath, temporaryPath, true);
            WritePresentation(temporaryPath, plan, analysis);

            using (var document = PresentationDocument.Open(temporaryPath, false))
            {
                var validationErrors = new OpenXmlValidator().Validate(document).Take(10).ToList();
                if (validationErrors.Count > 0)
                    return Failed("The generated PowerPoint presentation did not pass Open XML validation.");
            }

            File.Move(temporaryPath, outputPath, true);
            return new(outputPath, string.Empty);
        }
        catch (OpenXmlPackageException)
        {
            return Failed("The PowerPoint presentation could not be generated from this template.");
        }
        catch (IOException)
        {
            return Failed("The PowerPoint presentation could not be written to the selected location.");
        }
        catch (UnauthorizedAccessException)
        {
            return Failed("The PowerPoint presentation could not be written because access was denied.");
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (IOException)
                {
                    // The export result has already been decided; a leftover temporary file must not hide it.
                }
                catch (UnauthorizedAccessException)
                {
                    // The export result has already been decided; a leftover temporary file must not hide it.
                }
            }
        }
    }

    private static void WritePresentation(
        string presentationPath,
        SlideDeckPlan plan,
        PowerPointTemplateAnalysis analysis)
    {
        using var document = PresentationDocument.Open(presentationPath, true);
        var presentationPart = document.PresentationPart ?? throw new OpenXmlPackageException("The presentation part is missing.");
        var presentation = presentationPart.Presentation ?? throw new OpenXmlPackageException("The presentation root is missing.");
        var titleLayout = FindLayout(presentationPart, analysis.TitleLayoutUri) ?? throw new OpenXmlPackageException("The selected title layout is missing.");
        var contentLayout = FindLayout(presentationPart, analysis.ContentLayoutUri) ?? throw new OpenXmlPackageException("The selected content layout is missing.");

        RemoveExistingSlides(presentationPart, presentation);
        presentation.SlideIdList ??= new P.SlideIdList();

        uint slideId = 256;
        AppendSlide(
            presentationPart,
            presentation.SlideIdList,
            titleLayout,
            CreateTitleSlide(titleLayout, plan.Title),
            slideId++);

        foreach (var slide in plan.Slides)
        {
            AppendSlide(
                presentationPart,
                presentation.SlideIdList,
                contentLayout,
                CreateContentSlide(contentLayout, slide),
                slideId++);
        }

        presentation.Save();
    }

    private static SlideLayoutPart? FindLayout(PresentationPart presentationPart, string uri) => presentationPart
        .SlideMasterParts
        .SelectMany(master => master.SlideLayoutParts)
        .FirstOrDefault(layout => string.Equals(layout.Uri.ToString(), uri, StringComparison.Ordinal));

    private static void RemoveExistingSlides(PresentationPart presentationPart, P.Presentation presentation)
    {
        if (presentation.SlideIdList is not null)
        {
            foreach (var slideId in presentation.SlideIdList.Elements<P.SlideId>().ToList())
            {
                var relationshipId = slideId.RelationshipId?.Value;
                slideId.Remove();
                if (!string.IsNullOrEmpty(relationshipId) && presentationPart.TryGetPartById(relationshipId, out var part))
                    presentationPart.DeletePart(part);
            }
        }

        presentation.CustomShowList?.Remove();
    }

    private static P.Slide CreateTitleSlide(SlideLayoutPart layout, string title)
    {
        var titleShape = FindShape(layout, P.PlaceholderValues.CenteredTitle, P.PlaceholderValues.Title);
        var clonedTitle = CloneShape(titleShape, 2, [title]);
        return CreateSlide(clonedTitle);
    }

    private static P.Slide CreateContentSlide(SlideLayoutPart layout, SlideDeckPlanSlide slide)
    {
        var titleShape = FindShape(layout, P.PlaceholderValues.Title, P.PlaceholderValues.CenteredTitle);
        var contentShape = FindShape(layout, P.PlaceholderValues.Body, P.PlaceholderValues.Object);
        var clonedTitle = CloneShape(titleShape, 2, [slide.Title]);
        var clonedContent = CloneShape(contentShape, 3, slide.BulletPoints);
        return CreateSlide(clonedTitle, clonedContent);
    }

    private static P.Shape FindShape(SlideLayoutPart layout, params P.PlaceholderValues[] preferredTypes)
    {
        var shapes = layout.SlideLayout?.CommonSlideData?.ShapeTree?.Elements<P.Shape>().ToList() ?? [];
        foreach (var preferredType in preferredTypes)
        {
            var match = shapes.FirstOrDefault(shape => shape.TextBody is not null && GetPlaceholderType(shape) == preferredType);
            if (match is not null)
                return match;
        }

        throw new OpenXmlPackageException("A required placeholder shape is missing.");
    }

    private static P.PlaceholderValues? GetPlaceholderType(P.Shape shape) => shape
        .NonVisualShapeProperties?
        .ApplicationNonVisualDrawingProperties?
        .GetFirstChild<P.PlaceholderShape>()?
        .Type?
        .Value;

    private static P.Shape CloneShape(P.Shape source, uint id, IReadOnlyList<string> paragraphs)
    {
        var shape = (P.Shape)source.CloneNode(true);
        var drawingProperties = shape.NonVisualShapeProperties?.NonVisualDrawingProperties
            ?? throw new OpenXmlPackageException("A placeholder has no drawing properties.");
        drawingProperties.Id = id;

        var textBody = shape.TextBody ?? throw new OpenXmlPackageException("A placeholder has no text body.");
        var templateParagraph = textBody.Elements<A.Paragraph>().FirstOrDefault();
        var paragraphProperties = templateParagraph?.ParagraphProperties;
        var runProperties = templateParagraph?.Elements<A.Run>().FirstOrDefault()?.RunProperties;
        textBody.RemoveAllChildren<A.Paragraph>();

        foreach (var text in paragraphs)
        {
            var paragraph = new A.Paragraph();
            if (paragraphProperties is not null)
            {
                var clonedProperties = (A.ParagraphProperties)paragraphProperties.CloneNode(true);
                clonedProperties.Level = 0;
                clonedProperties.RemoveAllChildren<A.NoBullet>();
                paragraph.Append(clonedProperties);
            }

            var run = new A.Run();
            if (runProperties is not null)
                run.Append((A.RunProperties)runProperties.CloneNode(true));
            run.Append(new A.Text(text.Trim()));
            paragraph.Append(run);
            textBody.Append(paragraph);
        }

        return shape;
    }

    private static P.Slide CreateSlide(params P.Shape[] shapes)
    {
        var shapeTree = new P.ShapeTree(
            new P.NonVisualGroupShapeProperties(
                new P.NonVisualDrawingProperties { Id = 1, Name = string.Empty },
                new P.NonVisualGroupShapeDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.GroupShapeProperties(
                new A.TransformGroup(
                    new A.Offset { X = 0, Y = 0 },
                    new A.Extents { Cx = 0, Cy = 0 },
                    new A.ChildOffset { X = 0, Y = 0 },
                    new A.ChildExtents { Cx = 0, Cy = 0 })));
        shapeTree.Append(shapes);

        return new P.Slide(
            new P.CommonSlideData(shapeTree),
            new P.ColorMapOverride(new A.MasterColorMapping()));
    }

    private static void AppendSlide(
        PresentationPart presentationPart,
        P.SlideIdList slideIdList,
        SlideLayoutPart layout,
        P.Slide slide,
        uint id)
    {
        var slidePart = presentationPart.AddNewPart<SlidePart>();
        slidePart.Slide = slide;
        slidePart.AddPart(layout);
        slide.Save();

        slideIdList.Append(new P.SlideId
        {
            Id = id,
            RelationshipId = presentationPart.GetIdOfPart(slidePart),
        });
    }

    private static string EnsureExtension(string path) => string.Equals(Path.GetExtension(path), ".pptx", StringComparison.OrdinalIgnoreCase)
        ? path
        : $"{path}.pptx";

    private static bool PathsEqual(string first, string second) => string.Equals(
        Path.GetFullPath(first),
        Path.GetFullPath(second),
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static PowerPointRenderResult Failed(string issue) => new(string.Empty, issue);
}
