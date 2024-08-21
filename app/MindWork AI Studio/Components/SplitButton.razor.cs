using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class SplitButton<T> : ComponentBase
{
    [Parameter]
    public Color Color { get; set; } = Color.Default;
    
    [Parameter]
    public string Icon { get; set; } = string.Empty;

    [Parameter]
    public IReadOnlyCollection<T> Items { get; set; } = [];
    
    [Parameter]
    public Func<T, string> NameFunc { get; set; } = _ => string.Empty;
    
    [Parameter]
    public T? PreselectedValue { get; set; }
    
    [Parameter]
    public Func<T?, Task> OnClick { get; set; } = _ => Task.CompletedTask;
    
    /// <summary>
    /// What happens when the user selects an item by the dropdown?
    /// Immediate = true means that the OnClick event is triggered immediately
    /// after the user selects an item. Immediate = false means that the OnClick
    /// event is triggered only when the user clicks the button.
    /// </summary>
    [Parameter]
    public bool Immediate { get; set; }

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        if(this.PreselectedValue is not null)
            this.selectedValue = this.PreselectedValue;
        else
            this.selectedValue = this.Items.FirstOrDefault();
        
        await base.OnInitializedAsync();
    }

    #endregion

    private T? selectedValue;
    
    private string SelectedValueName()
    {
        if(this.selectedValue is null)
            return "Select...";
        
        return this.NameFunc(this.selectedValue!);
    }
    
    private void SelectItem(T item)
    {
        this.selectedValue = item;
        if(this.Immediate)
            this.OnClick(this.selectedValue);
    }
}