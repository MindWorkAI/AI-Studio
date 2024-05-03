namespace MudBlazor;

/// <summary>
/// For some reason MudExpansionPanels eternally tried to dispose all panels, therefore, RenderFragment was called infinitely<br/>
/// Created this component in order to bypass that weird behaviour
/// </summary>
internal sealed class MudMarkdownDetails : ComponentBase
{
	private int _elementIndex;

	[Parameter]
	public RenderFragment? TitleContent { get; set; }

	[Parameter]
	public RenderFragment? ChildContent { get; set; }

	private bool IsExpanded { get; set; }

	private string IconClasses => new CssBuilder("mud-expand-panel-icon")
		.AddClass("mud-transform", IsExpanded)
		.Build();

	protected override void BuildRenderTree(RenderTreeBuilder builder)
	{
		_elementIndex = 0;

		builder.OpenElement(_elementIndex++, "div");
		builder.AddAttribute(_elementIndex++, "class", "mud-expand-panel mud-elevation-1 mud-expand-panel-border");

		BuildTitle(builder);
		BuildContent(builder);

		builder.CloseElement();
	}

	private void BuildTitle(RenderTreeBuilder builder)
	{
		builder.OpenElement(_elementIndex++, "div");
		builder.AddAttribute(_elementIndex++, "class", "mud-expand-panel-header mud-ripple");
		builder.AddAttribute(_elementIndex++, "onclick", EventCallback.Factory.Create(this, OnHeaderClick));

		// Text
		builder.OpenElement(_elementIndex++, "div");
		builder.AddAttribute(_elementIndex++, "class", "mud-expand-panel-text");
		builder.AddContent(_elementIndex++, TitleContent);
		builder.CloseElement();

		// Collapse icon
		builder.OpenComponent<MudIcon>(_elementIndex++);
		builder.AddAttribute(_elementIndex++, nameof(MudIcon.Icon), Icons.Material.Filled.ExpandMore);
		builder.AddAttribute(_elementIndex++, "class", IconClasses);
		builder.CloseComponent();

		builder.CloseElement();
	}

	private void BuildContent(RenderTreeBuilder builder)
	{
		builder.OpenComponent<MudCollapse>(_elementIndex++);
		builder.AddAttribute(_elementIndex++, nameof(MudCollapse.Expanded), IsExpanded);

		builder.AddAttribute(_elementIndex++, nameof(MudCollapse.ChildContent), (RenderFragment)(contentBuilder =>
		{
			contentBuilder.OpenElement(_elementIndex++, "div");
			contentBuilder.AddAttribute(_elementIndex++, "class", "mud-expand-panel-content");
			contentBuilder.AddContent(_elementIndex++, ChildContent);
			contentBuilder.CloseElement();
		}));

		builder.CloseComponent();
	}

	private void OnHeaderClick()
	{
		IsExpanded = !IsExpanded;
	}
}
