namespace MudBlazor;

public static class ServiceCollectionEx
{
	public static IServiceCollection AddMudMarkdownServices(this IServiceCollection @this)
	{
		return @this.AddScoped<IMudMarkdownThemeService, MudMarkdownThemeService>();
	}

	public static IServiceCollection AddMudMarkdownClipboardService<T>(this IServiceCollection @this)
		where T : class, IMudMarkdownClipboardService
	{
		return @this.AddScoped<IMudMarkdownClipboardService, T>();
	}
}
