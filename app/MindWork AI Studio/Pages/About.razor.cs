using System.Reflection;

using AIStudio.Components;
using AIStudio.Dialogs;
using AIStudio.Tools.Metadata;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

using SharedTools;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Pages;

public partial class About : MSGComponentBase
{
    [Inject]
    private RustService RustService { get; init; } = null!;
    
    [Inject]
    private IDialogService DialogService { get; init; } = null!;
    
    [Inject]
    private ISnackbar Snackbar { get; init; } = null!;
    
    private static readonly Assembly ASSEMBLY = Assembly.GetExecutingAssembly();
    private static readonly MetaDataAttribute META_DATA = ASSEMBLY.GetCustomAttribute<MetaDataAttribute>()!;
    private static readonly MetaDataArchitectureAttribute META_DATA_ARCH = ASSEMBLY.GetCustomAttribute<MetaDataArchitectureAttribute>()!;
    private static readonly MetaDataLibrariesAttribute META_DATA_LIBRARIES = ASSEMBLY.GetCustomAttribute<MetaDataLibrariesAttribute>()!;
    
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(About).Namespace, nameof(About));

    private string osLanguage = string.Empty;
    
    private static string VersionApp => $"MindWork AI Studio: v{META_DATA.Version} (commit {META_DATA.AppCommitHash}, build {META_DATA.BuildNum}, {META_DATA_ARCH.Architecture.ToRID().ToUserFriendlyName()})";
    
    private static string MudBlazorVersion => $"MudBlazor: v{META_DATA.MudBlazorVersion}";
    
    private static string TauriVersion => $"Tauri: v{META_DATA.TauriVersion}";
    
    private string OSLanguage => $"{T("User-language provided by the OS")}: '{this.osLanguage}'";
    
    private string VersionRust => $"{T("Used Rust compiler")}: v{META_DATA.RustVersion}";
    
    private string VersionDotnetRuntime => $"{T("Used .NET runtime")}: v{META_DATA.DotnetVersion}";
    
    private string VersionDotnetSdk => $"{T("Used .NET SDK")}: v{META_DATA.DotnetSdkVersion}";
    
    private string BuildTime => $"{T("Build time")}: {META_DATA.BuildTime}";
    
    private string VersionPdfium => $"{T("Used PDFium version")}: v{META_DATA_LIBRARIES.PdfiumVersion}";
    
    private string versionPandoc = TB("Determine Pandoc version, please wait...");
    private PandocInstallation pandocInstallation;

    private GetLogPathsResponse logPaths;
    
    private bool showEnterpriseConfigDetails;

    private IPluginMetadata? configPlug = PluginFactory.AvailablePlugins.FirstOrDefault(x => x.Type is PluginType.CONFIGURATION);

    /// <summary>
    /// Determines whether the enterprise configuration has details that can be shown/hidden.
    /// Returns true if there are details available, false otherwise.
    /// </summary>
    private bool HasEnterpriseConfigurationDetails
    {
        get
        {
            return EnterpriseEnvironmentService.CURRENT_ENVIRONMENT.IsActive switch
            {
                // Case 1: No enterprise config and no plugin - no details available
                false when this.configPlug is null => false,

                // Case 2: Enterprise config with plugin but no central management - has details
                false => true,

                // Case 3: Enterprise config active but no plugin - has details
                true when this.configPlug is null => true,

                // Case 4: Enterprise config active with plugin - has details
                true => true
            };
        }
    }
    
    #region Overrides of ComponentBase
    
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        
        this.osLanguage = await this.RustService.ReadUserLanguage();
        this.logPaths = await this.RustService.GetLogPaths();
        
        // Determine the Pandoc version may take some time, so we start it here
        // without waiting for the result:
        _ = this.DeterminePandocVersion();
    }

    #endregion

    #region Overrides of MSGComponentBase

    protected override async Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        switch (triggeredEvent)
        {
            case Event.PLUGINS_RELOADED:
                this.configPlug = PluginFactory.AvailablePlugins.FirstOrDefault(x => x.Type is PluginType.CONFIGURATION);
                await this.InvokeAsync(this.StateHasChanged);
                break;
        }
        
        await base.ProcessIncomingMessage(sendingComponent, triggeredEvent, data);
    }

    #endregion

    private async Task DeterminePandocVersion()
    {
        this.pandocInstallation = await Pandoc.CheckAvailabilityAsync(this.RustService, false);
        var pandocInstallationType = this.pandocInstallation.IsLocalInstallation 
            ? T("installed by AI Studio")
            : T("installation provided by the system");
        
        switch (this.pandocInstallation)
        {
            case { CheckWasSuccessful: true, IsAvailable: true }:
                this.versionPandoc = $"{this.T("Installed Pandoc version")}: v{this.pandocInstallation.Version} ({pandocInstallationType}) - {T("OK")}";
                break;
            
            case { CheckWasSuccessful: true, IsAvailable: false }:
                this.versionPandoc = $"{this.T("Installed Pandoc version")}: v{this.pandocInstallation.Version} ({pandocInstallationType}) - {T("this version does not met the requirements")}";
                break;
            
            default:
                this.versionPandoc = TB("Installed Pandoc version: Pandoc is not installed or not available.");
                break;
        }
        
        await this.InvokeAsync(this.StateHasChanged);
    }
    
    private string PandocButtonText
    {
        get
        {
            return this.pandocInstallation switch
            {
                { IsAvailable: true, CheckWasSuccessful: true } => this.T("Check Pandoc Installation"),
                { IsAvailable: false, CheckWasSuccessful: true } => this.T("Update Pandoc"),
                
                _ => this.T("Install Pandoc")
            };
        }
    }

    private async Task ShowPandocDialog()
    {
        var dialogReference = await this.DialogService.ShowAsync<PandocDialog>(T("Pandoc Installation"), DialogOptions.FULLSCREEN);
        await dialogReference.Result;
        await this.DeterminePandocVersion();
    }
    
    private void ToggleEnterpriseConfigDetails()
    {
        this.showEnterpriseConfigDetails = !this.showEnterpriseConfigDetails;
    }

    private async Task CopyStartupLogPath()
    {
        await this.RustService.CopyText2Clipboard(this.Snackbar, this.logPaths.LogStartupPath);
    }
    
    private async Task CopyAppLogPath()
    {
        await this.RustService.CopyText2Clipboard(this.Snackbar, this.logPaths.LogAppPath);
    }
    
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
}
