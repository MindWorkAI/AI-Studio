using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class ProcessComponent<T> : ComponentBase where T : struct, Enum
{
    [Parameter]
    public bool ShowProgressAnimation { get; set; }
    
    [Parameter]
    public ProcessStepValue StepValue { get; set; }
    
    private readonly Process<T> process = Process<T>.INSTANCE;
}