using System.Text;

namespace AIStudio.Tools;

public sealed class SlideImageContent(string base64Image) : ISlideContent
{
    public StringBuilder Base64Image => new(base64Image);
}