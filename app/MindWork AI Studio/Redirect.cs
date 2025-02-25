namespace AIStudio;

internal static class Redirect
{
    private const string CONTENT = "/_content/";
    private const string SYSTEM = "/system/";
    
    internal static async Task HandlerContentAsync(HttpContext context, Func<Task> nextHandler)
    {
        var path = context.Request.Path.Value;
        if(string.IsNullOrWhiteSpace(path))
        {
            await nextHandler();
            return;
        }
        
        #if DEBUG
        
        if (path.StartsWith(SYSTEM, StringComparison.InvariantCulture))
        {
            context.Response.Redirect(path.Replace(SYSTEM, CONTENT), true, true);
            return;
        }
        
        #else
        
        if (path.StartsWith(CONTENT, StringComparison.InvariantCulture))
        {
            context.Response.Redirect(path.Replace(CONTENT, SYSTEM), true, true);
            return;
        }

        #endif

        await nextHandler();
    }
    
}