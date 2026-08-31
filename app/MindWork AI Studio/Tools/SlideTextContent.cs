using System.Text;

namespace AIStudio.Tools;

public sealed class SlideTextContent(string textContent) : ISlideContent
{
    //
    // One builder per slide, created once: an expression-bodied property would hand out a fresh
    // builder on every access, so appending further text to a slide would write into a throwaway
    // object and the text would never reach the slide.
    //
    public StringBuilder Text { get; } = new(textContent);
}