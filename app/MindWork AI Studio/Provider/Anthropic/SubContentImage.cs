using AIStudio.Provider.OpenAI;

namespace AIStudio.Provider.Anthropic;

public record SubContentImage(SubContentType Type, ISubContentImageSource Source) : ISubContent
{
    //
    // The properties of the parameters, declared again to be settable. The source generation of System.Text.Json
    // sets every init-only property while it creates the object, also one missing in the JSON, so its value from
    // the parameterless constructor would get lost. Keep them first and in the order of the parameters, which is
    // the order they get written in:
    //
    public SubContentType Type { get; set; } = Type;
    public ISubContentImageSource Source { get; set; } = Source;

    public SubContentImage() : this(SubContentType.IMAGE, new SubContentImageUrl())
    {
    }
}