namespace AIStudio.Tools;

public static class Markdown
{
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
}