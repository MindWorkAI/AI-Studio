namespace AIStudio.Tools.PowerPoint;

internal static class TemplateSlideElementKindExtensions
{
    extension(TemplateSlideElementKind kind)
    {
        /// <summary>
        /// How the kind is called in the list of template slides the model reads.
        /// </summary>
        public string CatalogName => kind switch
        {
            TemplateSlideElementKind.TEXT_BOX => "text box",
            TemplateSlideElementKind.SHAPE => "shape",
            TemplateSlideElementKind.LINE => "line",
            TemplateSlideElementKind.PICTURE => "picture",
            TemplateSlideElementKind.TABLE => "table",
            TemplateSlideElementKind.CHART => "chart",
            TemplateSlideElementKind.DIAGRAM => "diagram",
            TemplateSlideElementKind.GROUP => "group",

            _ => "object",
        };
    }
}