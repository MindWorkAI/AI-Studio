namespace MudBlazor;

public sealed class MudMarkdownStyling
{
    public TableStyling Table { get; } = new();
    
    public sealed class TableStyling
    {
        /// <summary>
        /// If true, striped table rows will be used.
        /// </summary>
        public bool IsStriped { get; set; } = true;

        /// <summary>
        /// If true, table's cells will have left/right borders.
        /// </summary>
        public bool IsBordered { get; set; } = true;
        
        /// <summary>
        /// Child content of component.
        /// </summary>
        public int Elevation { set; get; } = 1;
    }
}
