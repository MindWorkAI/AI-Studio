using System.Text;

namespace AIStudio.Tools;

public sealed class SlideManager
{
    private readonly Dictionary<int, Slide> slides = new();

    public void AddSlide(ContentStreamPresentationMetadata metadata, string? content, bool extractImages = false)
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
                Position = slideNumber
            };
            
            createdSlide.Content.Add(slideText);
            
            //
            // Add image content to the slide?
            //
            if (addImage)
            {
                var img = ContentStreamSseHandler.BuildImage(image!.Id!);
                var slideImage = new SlideImageContent(img);
                createdSlide.Content.Add(slideImage);
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
            }
            
            // Add any image content?
            if (addImage)
            {
                var img = ContentStreamSseHandler.BuildImage(image!.Id!);
                var slideImage = new SlideImageContent(img);
                slide.Content.Add(slideImage);
            }
        }
    }

    public string? GetAllSlidesInOrder()
    {
        var content = new StringBuilder();
        foreach (var slide in this.slides.Values.Where(s => !s.Delivered).OrderBy(s => s.Position))
        {
            slide.Delivered = true;
            foreach (var text in slide.Content.OfType<SlideTextContent>())
            {
                content.AppendLine(text.Text.ToString());
                content.AppendLine();
            }

            foreach (var image in slide.Content.OfType<SlideImageContent>())
            {
                content.AppendLine(image.Base64Image.ToString());
                content.AppendLine();
            }
        }
        
        return content.Length > 0 ? content.ToString() : null;
    }
}