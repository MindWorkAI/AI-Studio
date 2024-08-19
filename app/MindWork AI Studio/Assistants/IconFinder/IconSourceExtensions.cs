namespace AIStudio.Assistants.IconFinder;

public static class IconSourceExtensions
{
    public static string Name(this IconSources iconSource) => iconSource switch
    {
        IconSources.FLAT_ICON => "Flaticon",
        IconSources.FONT_AWESOME => "Font Awesome",
        IconSources.MATERIAL_ICONS => "Material Icons",
        IconSources.FEATHER_ICONS => "Feather Icons",
        IconSources.BOOTSTRAP_ICONS => "Bootstrap Icons",
        IconSources.ICONS8 => "Icons8",
            
        _ => "Generic",
    };
    
    public static string Prompt(this IconSources iconSource) => iconSource switch
    {
        IconSources.FLAT_ICON => "My icon source is Flaticon.",
        IconSources.FONT_AWESOME => "I look for an icon on Font Awesome. Please provide just valid icon names. Valid icon names are using the format `fa-icon-name`.",
        IconSources.MATERIAL_ICONS => "I look for a Material icon. Please provide just valid icon names. Valid icon names are using the format `IconName`.",
        IconSources.FEATHER_ICONS => "My icon source is Feather Icons. Please provide just valid icon names. Valid icon names usiing the format `icon-name`.",
        IconSources.BOOTSTRAP_ICONS => "I look for an icon for Bootstrap. Please provide just valid icon names. Valid icon names are using the format `bi-icon-name`.",
        IconSources.ICONS8 => "I look for an icon on Icons8.",
            
        _ => string.Empty,
    };

    public static string URL(this IconSources iconSource) => iconSource switch
    {
        IconSources.FLAT_ICON => "https://www.flaticon.com/",
        IconSources.FONT_AWESOME => "https://fontawesome.com/",
        IconSources.MATERIAL_ICONS => "https://material.io/resources/icons/",
        IconSources.FEATHER_ICONS => "https://feathericons.com/",
        IconSources.BOOTSTRAP_ICONS => "https://icons.getbootstrap.com/",
        IconSources.ICONS8 => "https://icons8.com/",
            
        _ => string.Empty,
    };
}