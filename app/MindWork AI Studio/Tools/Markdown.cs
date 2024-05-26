namespace AIStudio.Tools;

public static class Markdown
{
    public static Typo OverrideHeaderTypo(Typo arg) => arg switch
    {
        Typo.h1 => Typo.h4,
        Typo.h2 => Typo.h5,
        Typo.h3 => Typo.h6,
        Typo.h4 => Typo.h6,
        Typo.h5 => Typo.h6,
        Typo.h6 => Typo.h6,
        
        _ => arg
    };
}