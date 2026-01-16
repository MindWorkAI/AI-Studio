using AIStudio.Provider.OpenAI;

namespace AIStudio.Provider.Anthropic;

public record SubContentImage(SubContentType Type, ISubContentImageSource Source) : ISubContent
{
    public SubContentImage() : this(SubContentType.IMAGE, new SubContentImageUrl())
    {
    }
}