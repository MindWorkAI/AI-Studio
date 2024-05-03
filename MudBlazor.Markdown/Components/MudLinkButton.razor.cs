namespace MudBlazor;

internal sealed class MudLinkButton : MudComponentBase
{
	private string Classname =>
		new CssBuilder("mud-typography mud-link")
			.AddClass($"mud-{Color.ToDescriptionString()}-text")
			.AddClass($"mud-link-underline-{Underline.ToDescriptionString()}")
			.AddClass($"mud-typography-{Typo.ToDescriptionString()}")
			.AddClass("mud-link-disabled", IsDisabled)
			.AddClass(Class)
			.Build();

	/// <summary>
	/// The color of the component. It supports the theme colors.
	/// </summary>
	[Parameter]
	public Color Color { get; set; } = Color.Primary;

	/// <summary>
	/// Typography variant to use.
	/// </summary>
	[Parameter]
	public Typo Typo { get; set; } = Typo.body1;

	/// <summary>
	/// Controls when the link should have an underline.
	/// </summary>
	[Parameter]
	public Underline Underline { get; set; } = Underline.Hover;

	/// <summary>
	/// If true, the navlink will be disabled.
	/// </summary>
	[Parameter]
	public bool IsDisabled { get; set; }

	/// <summary>
	/// Child content of component.
	/// </summary>
	[Parameter]
	public RenderFragment? ChildContent { get; set; }

	/// <summary>
	/// Command executed on click
	/// </summary>
	[Parameter]
	public ICommand? Command { get; set; }

	/// <summary>
	/// Parameter passed to the command
	/// </summary>
	[Parameter]
	public object? CommandParameter { get; set; }

	protected override void BuildRenderTree(RenderTreeBuilder builder)
	{
		var i = 0;

		builder.OpenElement(i++, "span");
		builder.AddAttribute(i++, "class", Classname);
		builder.AddAttribute(i++, "style", Style);
		builder.AddAttribute(i++, "onclick", EventCallback.Factory.Create(this, OnClick));
		builder.AddContent(i++, ChildContent);
		builder.CloseElement();
	}

	private void OnClick()
	{
		if (IsDisabled)
			return;

		if (Command?.CanExecute(CommandParameter) ?? false)
			Command.Execute(CommandParameter);
	}
}