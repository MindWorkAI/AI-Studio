using System.Text;

namespace AIStudio.Tools;

public sealed class SlideManager
{
    private readonly Dictionary<int, Slide> slides = new();

    public void AddSlide(ContentStreamPresentationMetadata metadata, string? content, int? tokenCount, bool extractImages = false)
    {
        var slideNumber = metadata.Presentation?.SlideNumber ?? 0;
        if(slideNumber is 0)
            return;
        
        var image = metadata.Presentation?.Image ?? null;
        var addImage = false;
        if (extractImages && image is not null)
        {
            var isEnd = ContentStreamSseHandler.ProcessImageSegment(image.Id!, image);
            if (isEnd)
                addImage = true;
        }
        
        if (!this.slides.TryGetValue(slideNumber, out var slide))
        {
            //
            // Case: No existing slide content for this slide number.
            //
            
            var contentBuilder = new StringBuilder();
            contentBuilder.AppendLine();
            contentBuilder.AppendLine($"# Slide {slideNumber}");
            
            // Add any text content to the slide?
            if(!string.IsNullOrWhiteSpace(content))
                contentBuilder.AppendLine(content);

            //
            // Add the text content to the slide:
            //
            var slideText = new SlideTextContent(contentBuilder.ToString());
            var createdSlide = new Slide
            {
                Delivered = false,
                Position = slideNumber,

                // The count of the text we just added. It travels with the slide, because the slide
                // is delivered long after this event:
                TokenCount = tokenCount
            };

            createdSlide.Content.Add(slideText);

            //
            // Add image content to the slide?
            //
            if (addImage)
            {
                var markdownImage = ContentStreamSseHandler.BuildImageMarkdown(image!.Id!, image.MediaType);
                if (markdownImage is not null)
                {
                    createdSlide.Content.Add(new SlideImageContent(markdownImage));

                    // The runtime counted the text of the slide, not the data URI we just added:
                    createdSlide.TokenCount = null;
                }
            }

            this.slides[slideNumber] = createdSlide;
        }
        else
        {
            //
            // Case: Existing slide content for this slide number.
            //
            
            // Add any text content?
            if (!string.IsNullOrWhiteSpace(content))
            {
                var textContent = slide.Content.OfType<SlideTextContent>().First();
                textContent.Text.AppendLine(content);
                slide.TokenCount = ContentStreamPendingContent.AddTokenCounts(slide.TokenCount, tokenCount);
            }

            // Add any image content?
            if (addImage)
            {
                var markdownImage = ContentStreamSseHandler.BuildImageMarkdown(image!.Id!, image.MediaType);
                if (markdownImage is not null)
                {
                    slide.Content.Add(new SlideImageContent(markdownImage));

                    // The runtime counted the text of the slide, not the data URI we just added:
                    slide.TokenCount = null;
                }
            }
        }
    }

    public ContentStreamPendingContent? GetAllSlidesInOrder()
    {
        var content = new StringBuilder();

        // Starts at zero and stays a number only as long as every slide contributes a count of its
        // own. One slide without one makes the total unknown, which is what the caller has to know:
        int? tokenCount = 0;

        foreach (var slide in this.slides.Values.Where(s => !s.Delivered).OrderBy(s => s.Position))
        {
            slide.Delivered = true;
            tokenCount = ContentStreamPendingContent.AddTokenCounts(tokenCount, slide.TokenCount);

            foreach (var text in slide.Content.OfType<SlideTextContent>())
            {
                content.AppendLine(text.Text.ToString());
                content.AppendLine();
            }

            foreach (var image in slide.Content.OfType<SlideImageContent>())
            {
                content.AppendLine(image.MarkdownImage);
                content.AppendLine();
            }
        }

        return content.Length > 0 ? new ContentStreamPendingContent(content.ToString(), tokenCount) : null;
    }
}