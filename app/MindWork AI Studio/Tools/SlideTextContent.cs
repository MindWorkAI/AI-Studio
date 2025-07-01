using System.Text;

namespace AIStudio.Tools;

public sealed class SlideTextContent(string textContent) : ISlideContent
{
    public StringBuilder Text => new(textContent);
}