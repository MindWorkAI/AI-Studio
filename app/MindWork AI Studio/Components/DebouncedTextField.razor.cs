using Microsoft.AspNetCore.Components;

using Timer = System.Timers.Timer;

namespace AIStudio.Components;

public partial class DebouncedTextField : MudComponentBase, IDisposable
{
    [Parameter]
    public string Label { get; set; } = string.Empty;
    
    [Parameter]
    public string Text { get; set; } =  string.Empty;
    
    [Parameter]
    public EventCallback<string> TextChanged { get; set; }
    
    [Parameter]
    public Func<string, Task> WhenTextChangedAsync { get; set; } = _ => Task.CompletedTask;
    
    [Parameter]
    public Action<string> WhenTextCanged { get; set; } = _ => { };
    
    [Parameter]
    public int Lines { get; set; } = 1;

    [Parameter]
    public int MaxLines { get; set; } = 1;

    [Parameter]
    public Dictionary<string, object?> Attributes { get; set; } = [];

    [Parameter]
    public Func<string, string?> ValidationFunc { get; set; } = _ => null;
    
    [Parameter]
    public string HelpText { get; set; } = string.Empty;
    
    [Parameter]
    public string Placeholder { get; set; } = string.Empty;
    
    [Parameter]
    public string Icon { get; set; } = string.Empty;
    
    [Parameter]
    public TimeSpan DebounceTime { get; set; } = TimeSpan.FromMilliseconds(800);
    
    [Parameter]
    public bool Disabled { get; set; }
    
    private readonly Timer debounceTimer = new();
    private string text = string.Empty;
    private string lastParameterText = string.Empty;
    private bool isInitialized;

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.text = this.Text;
        this.lastParameterText = this.Text;
        this.debounceTimer.AutoReset = false;
        this.debounceTimer.Interval = this.DebounceTime.TotalMilliseconds;
        this.debounceTimer.Elapsed += (_, _) =>
        {
            this.debounceTimer.Stop();
            this.InvokeAsync(async () => await this.TextChanged.InvokeAsync(this.text));
            this.InvokeAsync(async () => await this.WhenTextChangedAsync(this.text));
            this.InvokeAsync(() => this.WhenTextCanged(this.text));
        };
        
        this.isInitialized = true;
        await base.OnInitializedAsync();
    }
    
    protected override async Task OnParametersSetAsync()
    {
        // Ensure the timer uses the latest debouncing interval:
        if (!this.isInitialized)
            return;
        
        if(Math.Abs(this.debounceTimer.Interval - this.DebounceTime.TotalMilliseconds) > 1)
            this.debounceTimer.Interval = this.DebounceTime.TotalMilliseconds;
        
        // Only sync when the parent's parameter actually changed since the last change:
        if (this.Text != this.lastParameterText)
        {
            this.text = this.Text;
            this.lastParameterText = this.Text;
        }
        
        this.debounceTimer.Stop();
        this.debounceTimer.Start();
        
        await base.OnParametersSetAsync();
    }

    #endregion

    private void OnTextChanged(string value)
    {
        this.text = value;
        this.debounceTimer.Stop();
        this.debounceTimer.Start();
    }

    #region IDisposable

    public void Dispose()
    {
        try
        {
            this.debounceTimer.Stop();
            this.debounceTimer.Dispose();
        }
        catch
        {
            // ignore
        }
    }

    #endregion
}