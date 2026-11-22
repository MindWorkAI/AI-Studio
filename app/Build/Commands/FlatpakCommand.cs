using System.Diagnostics;
using System.Formats.Tar;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

using Build.Tools;

namespace Build.Commands;

/// <summary>
/// Builds the Flatpak of AI Studio and its Pandoc plugin in Docker, the way the CI of the Flatpak repository does.
/// </summary>
/// <remarks>
/// Neither macOS nor Windows has flatpak-builder. Docker brings the CI image of the Flatpak repository instead, so the
/// build runs alike on every host with Docker, be it Docker Desktop, OrbStack, or Docker on Linux. Files go in and out
/// by docker cp only, never through a bind mount, whose paths and file permissions differ between these hosts. The
/// build lives in a container and a volume of its own, which the command removes at the end, also after a failure or
/// Ctrl+C. The script Flatpak/build-in-container.sh does the work inside the container.
/// </remarks>
public sealed partial class FlatpakCommand
{
    private const string AI_STUDIO_REPOSITORY = "https://github.com/MindWorkAI/AI-Studio";
    private const string FLATPAK_REPOSITORY = "https://github.com/MindWorkAI/Flatpak";
    private const string MAIN_BRANCH = "refs/heads/main";
    private const string DOCKER_LABEL = "org.mindworkai.build=flatpak";
    private const string CONTAINER_SCRIPT_DIRECTORY = "Flatpak";
    private const string CONTAINER_SCRIPT_NAME = "build-in-container.sh";
    private const string CONTAINER_INPUT_DIRECTORY = "input";
    private const string CONTAINER_BUILD_DIRECTORY = "/build";
    private const string CONTAINER_OUTPUT_DIRECTORY = "/out";

    /// <summary>
    /// The exit code after Ctrl+C, as shells report a process which SIGINT ended.
    /// </summary>
    private const int CANCELLED_EXIT_CODE = 130;

    private const UnixFileMode EXECUTABLE_MODE =UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
    private static readonly TimeSpan CONNECT_TIMEOUT = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan REQUEST_TIMEOUT = TimeSpan.FromSeconds(60);

    //
    // Without a handler of its own, HttpClient waits for a connection without any limit, see Pdfium.cs:
    //
    private static readonly HttpClient CLIENT = new(new SocketsHttpHandler { ConnectTimeout = CONNECT_TIMEOUT })
    {
        Timeout = REQUEST_TIMEOUT
    };

    private readonly Lock cancellationLock = new();

    private bool isCancelled;
    private bool isContainerStarted;
    private string containerName = string.Empty;
    private string volumeName = string.Empty;

    [GeneratedRegex(@"^\s*image:\s*(\S+)\s*$", RegexOptions.Multiline)]
    private static partial Regex WorkflowImageRegex();

    [GeneratedRegex("^[0-9a-f]{40}$")]
    private static partial Regex CommitHashRegex();

    /// <summary>
    /// Defines the command "flatpak".
    /// </summary>
    public static Command CreateCommand()
    {
        var output = new Option<string>("--output", "-o")
        {
            Description = "The folder for the bundles of AI Studio and its Pandoc plugin, outside the repository, e.g., ~/Downloads/flatpak. The command creates it when it does not exist",
            Required = true,
        };

        var source = new Option<string>("--source", "-s") { Description = $"Which state of AI Studio goes into the Flatpak: {string.Join("; ", FlatpakSource.All.Select(candidate => $"{candidate.Name}, {candidate.Description}"))}" };
        source.AcceptOnlyFromAmong([.. FlatpakSource.All.Select(candidate => candidate.Name)]);

        var pullRequest = new Option<int?>("--pr", "-p") { Description = "The number of the pull request to build, e.g., 1219; implies --source pr" };

        var architecture = new Option<string>("--arch", "-a") { Description = "The CPU architecture of the Flatpak, aarch64 or x86_64. Defaults to the one of the Docker host; the other one needs an emulation such as Rosetta and takes much longer" };
        architecture.AcceptOnlyFromAmong(FlatpakArchitecture.AARCH64.FlatpakName, FlatpakArchitecture.X86_64.FlatpakName);

        var command = new Command("flatpak", "Build the Flatpak of AI Studio and its Pandoc plugin in Docker, the way a release builds them. Without --source or --pr, the command asks what to build") { output, source, pullRequest, architecture };
        command.SetAction((parseResult, _) => new FlatpakCommand().BuildFlatpak(parseResult.GetRequiredValue(output), parseResult.GetValue(source), parseResult.GetValue(pullRequest), parseResult.GetValue(architecture)));
        return command;
    }

    public async Task<int> BuildFlatpak(string output, string? sourceName, int? pullRequestOption, string? architectureName)
    {
        if (!Environment.IsWorkingDirectoryValid())
            return 1;

        var outputDirectory = PrepareOutputDirectory(output);
        if (outputDirectory is null)
            return 1;

        Console.WriteLine("==============================");
        Console.WriteLine("- Building the Flatpak of AI Studio in Docker ...");

        var workingDirectory = Directory.GetCurrentDirectory();
        var dockerVersion = await CommandRunner.ReadOutputAsync(workingDirectory, "docker", ["version", "--format", "{{.Server.Version}}"]);
        if (dockerVersion.ExitCode is CommandRunner.COULD_NOT_START)
            return 1;

        if (dockerVersion.ExitCode is not 0)
        {
            Console.WriteLine("- Error: Docker does not answer. Start Docker, e.g., Docker Desktop or OrbStack, and try again.");
            return 1;
        }

        //
        // The questions come before the command handles Ctrl+C itself: up to here, nothing needs to be cleaned
        // up, so Ctrl+C may end the command at once, also while it waits for an answer.
        //
        var source = DetermineSource(sourceName, pullRequestOption);
        if (source is FlatpakSource.NONE)
            return 1;

        int? pullRequestNumber = null;
        if (source is FlatpakSource.PULL_REQUEST)
        {
            pullRequestNumber = pullRequestOption ?? AskForPullRequestNumber();
            if (pullRequestNumber is null)
                return 1;

            if (pullRequestNumber <= 0)
            {
                Console.WriteLine($"- Error: {pullRequestNumber} is not the number of a pull request.");
                return 1;
            }
        }

        Console.CancelKeyPress += this.OnCancelKeyPress;
        try
        {
            return await this.BuildInDockerAsync(workingDirectory, dockerVersion.Text.Trim(), outputDirectory, source, pullRequestNumber, architectureName);
        }
        finally
        {
            Console.CancelKeyPress -= this.OnCancelKeyPress;
        }
    }

    private async Task<int> BuildInDockerAsync(string workingDirectory, string dockerVersion, string outputDirectory, FlatpakSource source, int? pullRequestNumber, string? architectureName)
    {
        var stopwatch = Stopwatch.StartNew();
        var architecture = await DetermineArchitectureAsync(workingDirectory, architectureName);
        if (architecture is FlatpakArchitecture.NONE || this.IsCancelled)
            return this.ExitCodeAfterFailure;

        var aiStudioCommit = pullRequestNumber is { } number
            ? await ResolveRemoteCommitAsync(workingDirectory, AI_STUDIO_REPOSITORY, $"refs/pull/{number}/head", $"no pull request #{number}")
            : await ResolveRemoteCommitAsync(workingDirectory, AI_STUDIO_REPOSITORY, MAIN_BRANCH, "no branch main");

        if (aiStudioCommit is null || this.IsCancelled)
            return this.ExitCodeAfterFailure;

        var aiStudioDescription = pullRequestNumber is { } pullRequest ? await DescribePullRequestAsync(pullRequest) : source.Description;
        var flatpakCommit = await ResolveRemoteCommitAsync(workingDirectory, FLATPAK_REPOSITORY, MAIN_BRANCH, "no branch main");
        if (flatpakCommit is null || this.IsCancelled)
            return this.ExitCodeAfterFailure;

        var image = await ReadCiImageAsync(flatpakCommit);
        if (image is null || this.IsCancelled)
            return this.ExitCodeAfterFailure;

        Console.WriteLine($"- Docker {dockerVersion} builds the Flatpak for {architecture.FlatpakName}:");
        Console.WriteLine($"   - AI Studio: {aiStudioDescription}, commit {aiStudioCommit}");
        Console.WriteLine($"   - Flatpak repository: main of MindWorkAI/Flatpak, commit {flatpakCommit}");
        Console.WriteLine($"   - Image: {image}");
        Console.WriteLine($"   - Output: {outputDirectory}");

        await ReportLeftoversAsync(workingDirectory);

        if (await CommandRunner.RunAsync(workingDirectory, "docker", ["pull", "--platform", architecture.DockerPlatform, image]) is not 0 || this.IsCancelled)
        {
            if (!this.IsCancelled)
                Console.WriteLine($"- Error: Docker could not pull the image {image} for {architecture.DockerPlatform}.");

            return this.ExitCodeAfterFailure;
        }

        var environment = new Dictionary<string, string>
        {
            ["ARCH"] = architecture.FlatpakName,
            ["SOURCE"] = source.Name,
            ["AI_STUDIO_URL"] = AI_STUDIO_REPOSITORY,
            ["AI_STUDIO_COMMIT"] = aiStudioCommit,
            ["FLATPAK_URL"] = FLATPAK_REPOSITORY,
            ["FLATPAK_COMMIT"] = flatpakCommit,
            ["APP_BUNDLE"] = architecture.AppBundleName,
            ["PANDOC_BUNDLE"] = architecture.PandocBundleName,
            ["BUILD_DIRECTORY"] = CONTAINER_BUILD_DIRECTORY,
            ["OUT_DIRECTORY"] = CONTAINER_OUTPUT_DIRECTORY,
        };

        var name = $"aistudio-flatpak-{architecture.FlatpakName}-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
        try
        {
            var exitCode = await this.BuildInContainerAsync(workingDirectory, name, image, architecture, environment, outputDirectory);
            if (exitCode is 0)
                Console.WriteLine($"- Done after {(int)stopwatch.Elapsed.TotalMinutes} min {stopwatch.Elapsed.Seconds} s.");

            return exitCode;
        }
        finally
        {
            await this.RemoveContainerAndVolumeAsync(workingDirectory);
        }
    }

    private async Task<int> BuildInContainerAsync(string workingDirectory, string name, string image, FlatpakArchitecture architecture, IReadOnlyDictionary<string, string> environment, string outputDirectory)
    {
        var volume = await CommandRunner.ReadOutputAsync(workingDirectory, "docker", ["volume", "create", "--label", DOCKER_LABEL, name]);
        if (volume.ExitCode is not 0)
        {
            Console.WriteLine($"- Error: Docker could not create the volume {name}.");
            return 1;
        }

        lock (this.cancellationLock)
            this.volumeName = name;

        List<string> arguments = ["create", "--name", name, "--label", DOCKER_LABEL, "--init", "--privileged", "--platform", architecture.DockerPlatform, "--volume", $"{name}:{CONTAINER_BUILD_DIRECTORY}"];
        foreach (var (key, value) in environment)
            arguments.AddRange(["--env", $"{key}={value}"]);

        arguments.AddRange([image, "bash", $"/{CONTAINER_INPUT_DIRECTORY}/{CONTAINER_SCRIPT_NAME}"]);

        var container = await CommandRunner.ReadOutputAsync(workingDirectory, "docker", arguments);
        if (container.ExitCode is not 0)
        {
            Console.WriteLine($"- Error: Docker could not create the container {name}.");
            return 1;
        }

        lock (this.cancellationLock)
            this.containerName = name;

        Console.WriteLine($"- Created the container and the volume {name}.");
        var script = await ReadContainerScriptAsync();
        if (await CommandRunner.RunAsync(workingDirectory, "docker", ["cp", "-", $"{name}:/"], stream => WriteScriptArchiveAsync(stream, script)) is not 0)
        {
            Console.WriteLine($"- Error: Docker could not copy the build script into the container {name}.");
            return 1;
        }

        lock (this.cancellationLock)
        {
            if (this.isCancelled)
                return CANCELLED_EXIT_CODE;

            this.isContainerStarted = true;
        }

        //
        // docker start returns early when it gets a signal of its own, such as the Ctrl+C of the terminal, and the
        // container may still be running then. Only the state of the container tells whether the build ended, and how:
        //
        var startExitCode = await CommandRunner.RunAsync(workingDirectory, "docker", ["start", "--attach", name]);
        var buildExitCode = this.IsCancelled ? null : await ReadBuildExitCodeAsync(workingDirectory, name);
        if (this.IsCancelled)
        {
            Console.WriteLine("- The build was cancelled.");
            return CANCELLED_EXIT_CODE;
        }

        if (buildExitCode is null)
        {
            Console.WriteLine($"- Error: The container {name} did not finish its build; docker start ended with exit code {startExitCode}.");
            return 1;
        }

        if (buildExitCode is not 0)
        {
            Console.WriteLine($"- Error: The build in the container failed with exit code {buildExitCode}. Its output above says why.");
            return 1;
        }

        //
        // Bundles of an earlier build would otherwise pass for those of this one, should the container
        // not deliver one of them:
        //
        foreach (var bundleName in new[] { architecture.AppBundleName, architecture.PandocBundleName })
            File.Delete(Path.Join(outputDirectory, bundleName));

        if (await CommandRunner.RunAsync(workingDirectory, "docker", ["cp", $"{name}:{CONTAINER_OUTPUT_DIRECTORY}/.", outputDirectory]) is not 0)
        {
            Console.WriteLine($"- Error: Docker could not copy the bundles out of the container {name}.");
            return 1;
        }

        return await ReportBundlesAsync(outputDirectory, architecture) ? 0 : 1;
    }

    /// <summary>
    /// Reads how the build in the container ended.
    /// </summary>
    /// <returns>The exit code of the build script, or null when the container has not run to its end.</returns>
    private static async Task<int?> ReadBuildExitCodeAsync(string workingDirectory, string name)
    {
        var state = await CommandRunner.ReadOutputAsync(workingDirectory, "docker", ["inspect", "--format", "{{.State.Status}} {{.State.ExitCode}}", name]);
        var parts = state.Text.Trim().Split(' ');
        if (state.ExitCode is not 0 || parts.Length is not 2 || parts[0] is not "exited" || !int.TryParse(parts[1], out var exitCode))
            return null;

        return exitCode;
    }

    private bool IsCancelled
    {
        get
        {
            lock (this.cancellationLock)
                return this.isCancelled;
        }
    }

    private int ExitCodeAfterFailure => this.IsCancelled ? CANCELLED_EXIT_CODE : 1;

    /// <summary>
    /// Stops the build at the first Ctrl+C, so that the command can still remove its container and volume.
    /// </summary>
    /// <remarks>
    /// The shell inside the container waits for flatpak-builder and passes the signal on only after that, so the
    /// container gets killed instead. A second Ctrl+C ends the command at once and names what it leaves behind.
    /// </remarks>
    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs eventArgs)
    {
        string container;
        bool isStarted;
        lock (this.cancellationLock)
        {
            if (this.isCancelled)
            {
                Console.WriteLine("- Quitting at once.");
                WriteRemovalCommands(this.containerName, this.volumeName);
                return;
            }

            this.isCancelled = true;
            container = this.containerName;
            isStarted = this.isContainerStarted;
        }

        eventArgs.Cancel = true;
        Console.WriteLine("- Cancelling: the build stops, and the command removes its container and volume. Press Ctrl+C again to quit at once.");
        if (isStarted)
            _ = CommandRunner.ReadOutputAsync(Directory.GetCurrentDirectory(), "docker", ["kill", container]);
    }

    private async Task RemoveContainerAndVolumeAsync(string workingDirectory)
    {
        string container;
        string volume;
        lock (this.cancellationLock)
        {
            container = this.containerName;
            volume = this.volumeName;
        }

        var leftContainer = string.Empty;
        if (!string.IsNullOrWhiteSpace(container) && (await CommandRunner.ReadOutputAsync(workingDirectory, "docker", ["rm", "--force", container])).ExitCode is not 0)
            leftContainer = container;

        var leftVolume = string.Empty;
        if (!string.IsNullOrWhiteSpace(volume) && (await CommandRunner.ReadOutputAsync(workingDirectory, "docker", ["volume", "rm", volume])).ExitCode is not 0)
            leftVolume = volume;

        if (string.IsNullOrWhiteSpace(leftContainer) && string.IsNullOrWhiteSpace(leftVolume))
        {
            if (!string.IsNullOrWhiteSpace(volume))
                Console.WriteLine($"- Removed the container and the volume {volume}.");

            return;
        }

        Console.WriteLine("- Error: Docker could not remove everything this build created.");
        WriteRemovalCommands(leftContainer, leftVolume);
    }

    private static void WriteRemovalCommands(string container, string volume)
    {
        if (string.IsNullOrWhiteSpace(container) && string.IsNullOrWhiteSpace(volume))
            return;

        Console.WriteLine("   Remove what is left with:");
        if (!string.IsNullOrWhiteSpace(container))
            Console.WriteLine($"   docker rm --force {container}");

        if (!string.IsNullOrWhiteSpace(volume))
            Console.WriteLine($"   docker volume rm {volume}");
    }

    /// <summary>
    /// Resolves the output folder and creates it when it does not exist.
    /// </summary>
    /// <remarks>
    /// A folder inside the repository is refused: the bundles would show up in git status, and a build of the local
    /// checkout would copy them into the container.
    /// </remarks>
    /// <param name="output">The folder as the user gave it; a leading ~ stands for the home folder.</param>
    /// <returns>The absolute path of the folder, or null when it cannot be used.</returns>
    private static string? PrepareOutputDirectory(string output)
    {
        var outputDirectory = Path.GetFullPath(ExpandHomeDirectory(output));
        var repositoryDirectory = Environment.GetRepositoryDirectory();
        var relativePath = Path.GetRelativePath(repositoryDirectory, outputDirectory);
        var isOutsideRepository = Path.IsPathRooted(relativePath) || relativePath == ".." || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
        if (!isOutsideRepository)
        {
            Console.WriteLine($"- Error: The output folder '{outputDirectory}' lies inside the repository. The bundles would show up in git status, and a build of the local checkout would copy them into the container.");
            Console.WriteLine($"   Choose a folder outside of '{repositoryDirectory}', e.g., --output ~/Downloads/flatpak.");
            return null;
        }

        try
        {
            Directory.CreateDirectory(outputDirectory);
            return outputDirectory;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"- Error: Could not create the output folder '{outputDirectory}': {exception.Message}");
            return null;
        }
    }

    private static string ExpandHomeDirectory(string path)
    {
        var homeDirectory = global::System.Environment.GetFolderPath(global::System.Environment.SpecialFolder.UserProfile);
        if (path == "~")
            return homeDirectory;

        if (path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith(@"~\", StringComparison.Ordinal))
            return Path.Join(homeDirectory, path[2..]);

        return path;
    }

    private static async Task<FlatpakArchitecture> DetermineArchitectureAsync(string workingDirectory, string? architectureName)
    {
        if (!string.IsNullOrWhiteSpace(architectureName))
            return FlatpakArchitecture.FromName(architectureName);

        var dockerInfo = await CommandRunner.ReadOutputAsync(workingDirectory, "docker", ["info", "--format", "{{.Architecture}}"]);
        if (dockerInfo.ExitCode is not 0)
        {
            Console.WriteLine("- Error: Docker did not name the architecture of its host. Choose one with --arch aarch64 or --arch x86_64.");
            return FlatpakArchitecture.NONE;
        }

        var architecture = FlatpakArchitecture.FromName(dockerInfo.Text);
        if (architecture is FlatpakArchitecture.NONE)
            Console.WriteLine($"- Error: The Docker host runs on '{dockerInfo.Text.Trim()}', for which there is no Flatpak. Choose one with --arch aarch64 or --arch x86_64.");

        return architecture;
    }

    /// <summary>
    /// Asks a repository on GitHub which commit a reference points to.
    /// </summary>
    /// <remarks>
    /// The command fixes every commit before the build starts. A push during the build therefore cannot mix two
    /// states, and the output names exactly what the Flatpak contains.
    /// </remarks>
    /// <param name="workingDirectory">Where git should run.</param>
    /// <param name="repository">The URL of the repository.</param>
    /// <param name="reference">The full name of the reference, e.g., refs/heads/main.</param>
    /// <param name="whatIsMissing">What the repository lacks when the reference does not exist, e.g., "no branch main".</param>
    /// <returns>The hash of the commit, or null when the repository has no such reference.</returns>
    private static async Task<string?> ResolveRemoteCommitAsync(string workingDirectory, string repository, string reference, string whatIsMissing)
    {
        var references = await CommandRunner.ReadOutputAsync(workingDirectory, "git", ["ls-remote", repository, reference]);
        if (references.ExitCode is not 0)
        {
            Console.WriteLine($"- Error: Could not ask {repository} for '{reference}'.");
            return null;
        }

        foreach (var line in references.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split('\t');
            if (parts.Length is 2 && parts[1] == reference && CommitHashRegex().IsMatch(parts[0]))
                return parts[0];
        }

        Console.WriteLine($"- Error: {repository} has {whatIsMissing}.");
        return null;
    }

    /// <summary>
    /// Decides which state of AI Studio goes into the Flatpak, and asks when the options leave it open.
    /// </summary>
    /// <returns>The source, or NONE when the options contradict each other or nobody answered.</returns>
    private static FlatpakSource DetermineSource(string? sourceName, int? pullRequestOption)
    {
        var source = string.IsNullOrWhiteSpace(sourceName) ? FlatpakSource.NONE : FlatpakSource.FromName(sourceName);
        if (pullRequestOption is not null)
        {
            if (source is FlatpakSource.NONE or FlatpakSource.PULL_REQUEST)
                return FlatpakSource.PULL_REQUEST;

            Console.WriteLine($"- Error: --pr builds a pull request, while --source {source.Name} builds something else. Leave out one of them.");
            return FlatpakSource.NONE;
        }

        return source is FlatpakSource.NONE ? AskForSource() : source;
    }

    private static FlatpakSource AskForSource()
    {
        var sources = FlatpakSource.All;
        Console.WriteLine("- Which state of AI Studio should go into the Flatpak?");
        for (var index = 0; index < sources.Count; index++)
            Console.WriteLine($"   {index + 1}) {sources[index].Description}");

        while (true)
        {
            Console.Write($"- Your choice (1-{sources.Count}): ");
            var answer = Console.ReadLine();
            if (answer is null)
            {
                Console.WriteLine();
                Console.WriteLine($"- Error: No answer arrived. Choose with --source {string.Join(", --source ", sources.Select(candidate => candidate.Name))}, or with --pr <number>.");
                return FlatpakSource.NONE;
            }

            if (int.TryParse(answer.Trim(), out var choice) && choice >= 1 && choice <= sources.Count)
                return sources[choice - 1];

            Console.WriteLine($"   Please enter a number from 1 to {sources.Count}.");
        }
    }

    private static int? AskForPullRequestNumber()
    {
        while (true)
        {
            Console.Write("- Number of the pull request: ");
            var answer = Console.ReadLine();
            if (answer is null)
            {
                Console.WriteLine();
                Console.WriteLine("- Error: No answer arrived. Pass the number with --pr <number>.");
                return null;
            }

            if (int.TryParse(answer.Trim().TrimStart('#'), out var number) && number > 0)
                return number;

            Console.WriteLine("   Please enter the number of a pull request, e.g., 1219.");
        }
    }

    /// <summary>
    /// Describes a pull request with its title and state, so that a mistyped number stands out before the build.
    /// </summary>
    /// <remarks>
    /// The commit comes from git, which needs no token and knows no rate limit. The title is a courtesy on top: when
    /// the GitHub API refuses to answer, e.g., after too many requests without a token, the build goes on without it.
    /// </remarks>
    private static async Task<string> DescribePullRequestAsync(int number)
    {
        var description = $"pull request #{number} of MindWorkAI/AI-Studio";
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/MindWorkAI/AI-Studio/pulls/{number}");
            request.Headers.UserAgent.ParseAdd("MindWork-AI-Studio-Build-Script");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");

            using var response = await CLIENT.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return $"{description} (GitHub did not tell its title: {(int)response.StatusCode} {response.ReasonPhrase})";

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var json = await JsonDocument.ParseAsync(stream);
            var root = json.RootElement;
            var title = root.GetProperty("title").GetString();
            var state = root.GetProperty("merged").GetBoolean() ? "merged" : root.GetProperty("state").GetString() switch
            {
                "open" => "open",
                _ => "closed without merging",
            };

            return $"{description}, \"{title}\" ({state})";
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return $"{description} (GitHub did not tell its title: {exception.Message})";
        }
    }

    /// <summary>
    /// Reads which container image the CI of the Flatpak repository builds with.
    /// </summary>
    /// <remarks>
    /// The image carries the GNOME runtime the manifest builds against. Reading it from the workflow keeps the local
    /// build in step with the CI, e.g., when the Flatpak moves on to a newer GNOME runtime.
    /// </remarks>
    /// <param name="flatpakCommit">The commit of the Flatpak repository whose workflow counts.</param>
    /// <returns>The image, or null when the workflow cannot be read or names more or less than one image.</returns>
    private static async Task<string?> ReadCiImageAsync(string flatpakCommit)
    {
        var url = $"https://raw.githubusercontent.com/MindWorkAI/Flatpak/{flatpakCommit}/.github/workflows/flatpak.yml";
        string workflow;
        try
        {
            workflow = await CLIENT.GetStringAsync(url);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            Console.WriteLine($"- Error: Could not read the CI workflow of the Flatpak repository from {url}: {exception.Message}");
            return null;
        }

        var images = WorkflowImageRegex().Matches(workflow).Select(match => match.Groups[1].Value).Distinct().ToList();
        if (images.Count is 1)
            return images[0];

        Console.WriteLine($"- Error: The CI workflow of the Flatpak repository names {images.Count} container images instead of one: {url}");
        return null;
    }

    /// <summary>
    /// Names the containers and volumes which earlier builds left behind, e.g., after the terminal was closed.
    /// </summary>
    /// <remarks>
    /// They may also belong to a build which is still running in another terminal. Hence, the command only names
    /// them and leaves the removal to the user.
    /// </remarks>
    private static async Task ReportLeftoversAsync(string workingDirectory)
    {
        var containers = await ReadNamesAsync(workingDirectory, ["ps", "--all", "--filter", $"label={DOCKER_LABEL}", "--format", "{{.Names}}"]);
        var volumes = await ReadNamesAsync(workingDirectory, ["volume", "ls", "--filter", $"label={DOCKER_LABEL}", "--format", "{{.Name}}"]);
        if (containers.Count is 0 && volumes.Count is 0)
            return;

        Console.WriteLine("- Note: Docker still holds containers or volumes of earlier builds. They may belong to a build which is still running, so the command leaves them alone. Once no other build runs, remove them with:");
        foreach (var container in containers)
            Console.WriteLine($"   docker rm --force {container}");

        foreach (var volume in volumes)
            Console.WriteLine($"   docker volume rm {volume}");
    }

    private static async Task<IReadOnlyList<string>> ReadNamesAsync(string workingDirectory, IReadOnlyList<string> arguments)
    {
        var names = await CommandRunner.ReadOutputAsync(workingDirectory, "docker", arguments);
        return names.ExitCode is 0 ? names.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) : [];
    }

    /// <summary>
    /// Reads the script which builds inside the container, with LF line endings.
    /// </summary>
    /// <remarks>
    /// Git on Windows may check the script out with CR LF, which bash refuses.
    /// </remarks>
    private static async Task<byte[]> ReadContainerScriptAsync()
    {
        var path = Path.Join(Directory.GetCurrentDirectory(), CONTAINER_SCRIPT_DIRECTORY, CONTAINER_SCRIPT_NAME);
        var script = await File.ReadAllTextAsync(path, Encoding.UTF8);
        return Environment.UTF8_NO_BOM.GetBytes(script.ReplaceLineEndings("\n"));
    }

    /// <summary>
    /// Writes the build script as a tar archive, which docker cp unpacks into the container.
    /// </summary>
    /// <remarks>
    /// docker cp copies a single file only into a folder which already exists, so the archive brings its folder along.
    /// </remarks>
    private static async Task WriteScriptArchiveAsync(Stream stream, byte[] script)
    {
        await using var writer = new TarWriter(stream, TarEntryFormat.Pax, leaveOpen: true);
        await writer.WriteEntryAsync(new PaxTarEntry(TarEntryType.Directory, $"{CONTAINER_INPUT_DIRECTORY}/") { Mode = EXECUTABLE_MODE });

        using var data = new MemoryStream(script);
        await writer.WriteEntryAsync(new PaxTarEntry(TarEntryType.RegularFile, $"{CONTAINER_INPUT_DIRECTORY}/{CONTAINER_SCRIPT_NAME}") { Mode = EXECUTABLE_MODE, DataStream = data });
    }

    /// <summary>
    /// Names the bundles in the output folder, with their sizes and SHA-256 hashes.
    /// </summary>
    /// <returns>True, when both bundles are there.</returns>
    private static async Task<bool> ReportBundlesAsync(string outputDirectory, FlatpakArchitecture architecture)
    {
        Console.WriteLine("- The bundles:");
        foreach (var bundleName in new[] { architecture.AppBundleName, architecture.PandocBundleName })
        {
            var path = Path.Join(outputDirectory, bundleName);
            if (!File.Exists(path))
            {
                Console.WriteLine($"- Error: The container did not deliver '{bundleName}'.");
                return false;
            }

            await using var bundle = File.OpenRead(path);
            var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(bundle));
            Console.WriteLine($"   - {path}");
            Console.WriteLine($"     {bundle.Length / 1024 / 1024} MB, SHA-256 {hash}");
        }

        return true;
    }
}