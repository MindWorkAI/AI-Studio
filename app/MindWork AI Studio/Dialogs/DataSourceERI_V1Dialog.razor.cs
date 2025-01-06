using AIStudio.Settings.DataModel;

using Microsoft.AspNetCore.Components;

// ReSharper disable InconsistentNaming
namespace AIStudio.Dialogs;

public partial class DataSourceERI_V1Dialog : ComponentBase
{
    [Parameter]
    public bool IsEditing { get; set; }
    
    [Parameter]
    public DataSourceERI_V1 DataSource { get; set; }
}