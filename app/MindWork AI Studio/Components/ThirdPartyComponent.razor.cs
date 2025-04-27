using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class ThirdPartyComponent : MSGComponentBase
{
    [Parameter]
    public string Name { get; set; } = string.Empty;
    
    [Parameter]
    public string UseCase { get; set; } = string.Empty;
    
    [Parameter]
    public string Developer { get; set; } = string.Empty;
    
    [Parameter]
    public string LicenseName { get; set; } = string.Empty;
    
    [Parameter]
    public string LicenseUrl { get; set; } = string.Empty;
    
    [Parameter]
    public string RepositoryUrl { get; set; } = string.Empty;

    private string Header => $"{this.Name} ({this.Developer})";
}