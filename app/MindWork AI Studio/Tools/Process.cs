namespace AIStudio.Tools;

public sealed class Process<T> where T : struct, Enum
{
    public static readonly Process<T> INSTANCE = new();
    
    private readonly Dictionary<T, ProcessStepValue> stepsData = [];
    private readonly int min = int.MaxValue;
    private readonly int max = int.MinValue;
    private readonly string[] labels;

    private Process()
    {
        var values = Enum.GetValues<T>();
        this.labels = new string[values.Length];

        for (var i = 0; i < values.Length; i++)
        {
            var value = values[i];
            var stepValue = Convert.ToInt32(value);
            var stepName = ProcessStepTextRouter.GetText(value);

            this.labels[i] = stepName;
            this.stepsData[value] = new ProcessStepValue(stepValue, stepName);

            if (stepValue < this.min)
                this.min = stepValue;

            if (stepValue > this.max)
                this.max = stepValue;
        }
    }
    
    public string[] Labels => this.labels;
    
    public int Min => this.min;
    
    public int Max => this.max;
    
    public ProcessStepValue this[T step] => this.stepsData[step];
}