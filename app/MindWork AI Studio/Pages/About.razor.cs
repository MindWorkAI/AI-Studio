using System.Reflection;

using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Pages;

public partial class About : ComponentBase
{
    [Inject]
    private MessageBus MessageBus { get; init; } = null!;
    
    [Inject]
    private RustService RustService { get; init; } = null!;
    
    [Inject]
    private ISnackbar Snackbar { get; init; } = null!;
    
    private static readonly Assembly ASSEMBLY = Assembly.GetExecutingAssembly();
    private static readonly MetaDataAttribute META_DATA = ASSEMBLY.GetCustomAttribute<MetaDataAttribute>()!;
    
    private static string VersionDotnetRuntime => $"Used .NET runtime: v{META_DATA.DotnetVersion}";
    
    private static string VersionDotnetSdk => $"Used .NET SDK: v{META_DATA.DotnetSdkVersion}";
    
    private static string VersionRust => $"Used Rust compiler: v{META_DATA.RustVersion}";

    private static string VersionApp => $"MindWork AI Studio: v{META_DATA.Version} (commit {META_DATA.AppCommitHash}, build {META_DATA.BuildNum})";
    
    private static string BuildTime => $"Build time: {META_DATA.BuildTime}";
    
    private static string MudBlazorVersion => $"MudBlazor: v{META_DATA.MudBlazorVersion}";
    
    private static string TauriVersion => $"Tauri: v{META_DATA.TauriVersion}";

    private GetLogPathsResponse logPaths;

    #region Overrides of ComponentBase

    private async Task CopyStartupLogPath()
    {
        await this.RustService.CopyText2Clipboard(this.Snackbar, this.logPaths.LogStartupPath);
    }
    
    private async Task CopyAppLogPath()
    {
        await this.RustService.CopyText2Clipboard(this.Snackbar, this.logPaths.LogAppPath);
    }
    
    protected override async Task OnInitializedAsync()
    {
        this.logPaths = await this.RustService.GetLogPaths();
        await base.OnInitializedAsync();
    }

    #endregion
    
    private const string LICENSE = """
        # Functional Source License, Version 1.1, MIT Future License

        ## Abbreviation

        FSL-1.1-MIT

        ## Notice

        Copyright 2025 Thorsten Sommer

        ## Terms and Conditions

        ### Licensor ("We")

        The party offering the Software under these Terms and Conditions.

        ### The Software

        The "Software" is each version of the software that we make available under
        these Terms and Conditions, as indicated by our inclusion of these Terms and
        Conditions with the Software.

        ### License Grant

        Subject to your compliance with this License Grant and the Patents,
        Redistribution and Trademark clauses below, we hereby grant you the right to
        use, copy, modify, create derivative works, publicly perform, publicly display
        and redistribute the Software for any Permitted Purpose identified below.

        ### Permitted Purpose

        A Permitted Purpose is any purpose other than a Competing Use. A Competing Use
        means making the Software available to others in a commercial product or
        service that:

        1. substitutes for the Software;

        2. substitutes for any other product or service we offer using the Software
           that exists as of the date we make the Software available; or

        3. offers the same or substantially similar functionality as the Software.

        Permitted Purposes specifically include using the Software:

        1. for your internal use and access;

        2. for non-commercial education;

        3. for non-commercial research; and

        4. in connection with professional services that you provide to a licensee
           using the Software in accordance with these Terms and Conditions.

        ### Patents

        To the extent your use for a Permitted Purpose would necessarily infringe our
        patents, the license grant above includes a license under our patents. If you
        make a claim against any party that the Software infringes or contributes to
        the infringement of any patent, then your patent license to the Software ends
        immediately.

        ### Redistribution

        The Terms and Conditions apply to all copies, modifications and derivatives of
        the Software.

        If you redistribute any copies, modifications or derivatives of the Software,
        you must include a copy of or a link to these Terms and Conditions and not
        remove any copyright notices provided in or with the Software.

        ### Disclaimer

        THE SOFTWARE IS PROVIDED "AS IS" AND WITHOUT WARRANTIES OF ANY KIND, EXPRESS OR
        IMPLIED, INCLUDING WITHOUT LIMITATION WARRANTIES OF FITNESS FOR A PARTICULAR
        PURPOSE, MERCHANTABILITY, TITLE OR NON-INFRINGEMENT.

        IN NO EVENT WILL WE HAVE ANY LIABILITY TO YOU ARISING OUT OF OR RELATED TO THE
        SOFTWARE, INCLUDING INDIRECT, SPECIAL, INCIDENTAL OR CONSEQUENTIAL DAMAGES,
        EVEN IF WE HAVE BEEN INFORMED OF THEIR POSSIBILITY IN ADVANCE.

        ### Trademarks

        Except for displaying the License Details and identifying us as the origin of
        the Software, you have no right under these Terms and Conditions to use our
        trademarks, trade names, service marks or product names.

        ## Grant of Future License

        We hereby irrevocably grant you an additional license to use the Software under
        the MIT license that is effective on the second anniversary of the date we make
        the Software available. On or after that date, you may use the Software under
        the MIT license, in which case the following will apply:

        Permission is hereby granted, free of charge, to any person obtaining a copy of
        this software and associated documentation files (the "Software"), to deal in
        the Software without restriction, including without limitation the rights to
        use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
        of the Software, and to permit persons to whom the Software is furnished to do
        so, subject to the following conditions:

        The above copyright notice and this permission notice shall be included in all
        copies or substantial portions of the Software.

        THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
        IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
        FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
        AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
        LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
        OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
        SOFTWARE.
        """;
    
    private async Task CheckForUpdate()
    {
        await this.MessageBus.SendMessage<bool>(this, Event.USER_SEARCH_FOR_UPDATE);
    }
    
    // TODO: DELETE FOR DEBUGGING ONLY
    private bool isChecking;
    private string statusMessage = string.Empty;
    private async Task CheckPandoc()
    {
        this.isChecking = true;
        this.statusMessage = "Überprüfe die Verfügbarkeit von Pandoc...";
        this.StateHasChanged(); // Aktualisiere die UI
        var isPandocAvailable = await Pandoc.IsPandocAvailableAsync();
        if (isPandocAvailable)
        {
            this.statusMessage = "Pandoc ist verfügbar und erfüllt die Mindestversion.";
        }
        else
        {
            this.statusMessage = "Pandoc ist nicht verfügbar oder die installierte Version ist zu niedrig.";
        }
        this.isChecking = false;
        this.StateHasChanged(); // Aktualisiere die UI
    }

    private async Task InstallPandoc()
    {
        var installPandoc = Pandoc.InstallPandocAsync(this.RustService);
    }
}
