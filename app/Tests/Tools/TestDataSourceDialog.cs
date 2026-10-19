using System.Reflection;

using MudBlazor;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Controls a dialog result without rendering a Blazor circuit. The real dialog save handler and
/// the real parent edit handler still run, with the same parameters and result as in the UI.
/// </summary>
internal sealed class TestDataSourceDialog
{
    private readonly TaskCompletionSource<DialogResult?> result = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public IDialogService Service { get; }

    public IMudDialogInstance Instance { get; }

    public DialogParameters? Parameters { get; private set; }

    public Type? ComponentType { get; private set; }

    public string? Title { get; private set; }

    public int ShowCount { get; private set; }

    public TestDataSourceDialog()
    {
        var reference = Create<IDialogReference>((method, _) => method.Name == "get_Result"
            ? this.result.Task
            : throw new NotSupportedException(method.Name));
        this.Service = Create<IDialogService>((method, arguments) =>
        {
            if (method.Name != "ShowAsync")
                throw new NotSupportedException(method.Name);

            this.ShowCount++;
            this.ComponentType = method.GetGenericArguments().Single();
            this.Title = arguments!.OfType<string>().FirstOrDefault();
            this.Parameters = arguments.OfType<DialogParameters>().Single();
            return Task.FromResult(reference);
        });
        this.Instance = Create<IMudDialogInstance>((method, arguments) =>
        {
            switch (method.Name)
            {
                case "Close":
                    this.Complete(arguments!.OfType<DialogResult>().Single());
                    return null;
                case "Cancel":
                    this.Complete(DialogResult.Cancel());
                    return null;
                default:
                    throw new NotSupportedException(method.Name);
            }
        });
    }

    public void Complete(DialogResult dialogResult) => this.result.TrySetResult(dialogResult);

    private static T Create<T>(Func<MethodInfo, object?[]?, object?> invoke) where T : class
    {
        var proxy = DispatchProxy.Create<T, DialogCallProxy>();
        ((DialogCallProxy)(object)proxy).InvokeCall = invoke;
        return proxy;
    }
}

// DispatchProxy generates a subclass, so this type must be public and cannot be sealed.
public class DialogCallProxy : DispatchProxy
{
    public Func<MethodInfo, object?[]?, object?> InvokeCall { get; set; } = null!;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => this.InvokeCall(targetMethod!, args);
}
