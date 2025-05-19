# Building
You just want to use the app? Then simply [download the appropriate setup for your operating system](Setup.md). This chapter is intended for developers who want to modify and customize the code.

## Prefaces regarding Linux development systems
Unfortunately, we have to provide a note regarding development on Linux systems. MindWork AI Studio consists of a Rust and a .NET part. Compiling the .NET code works smoothly on all operating systems. However, this is not the case for our Rust part. More specifically, it is not the Rust code itself that is problematic, but rather the Tauri framework on which we base our work. Tauri has certain dependencies that depend on the operating system. The specific dependencies vary between different Linux distributions and between versions of distributions.

Therefore, we cannot provide a static list here that is valid for all Linux systems. Unfortunately, the situation is even more complex: Tauri requires dependencies that are not available in current Linux distributions because they already include newer versions. **For these reasons, we currently advise against developing AI Studio on Linux.** In case you still want to try, you will need a lot of patience and willingness to experiment. We ask for your understanding. Thank you very much.

## Prerequisites
1. Install the [.NET 9 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0).
2. [Install the Rust compiler](https://www.rust-lang.org/tools/install) in the latest stable version.
3. Met the prerequisites for building [Tauri](https://tauri.app/v1/guides/getting-started/prerequisites/). Node.js is **not** required, though.
4. The core team uses [JetBrains](https://www.jetbrains.com/) [Rider](https://www.jetbrains.com/rider/) and [RustRover](https://www.jetbrains.com/rust/) for development. Both IDEs are free to use for open-source projects for non-commercial use. They are available for macOS, Linux, and Windows systems. Profiles are provided for these IDEs, so you can get started right away. However, you can also use a different IDE.
4. Clone the repository.

## One-time mandatory steps
Regardless of whether you want to build the app locally for yourself (not trusting the pre-built binaries) or test your changes before creating a PR, you have to run the following commands at least once:

1. Open a terminal.
2. Install the Tauri CLI by running `cargo install --version 1.6.2 tauri-cli`.
3. Navigate to the `/app/Build` directory within the repository.
4. Run `dotnet run build` to build the entire app.

This is necessary because the build script and the Tauri framework assume that the .NET app is available as a so-called "sidecar." Although the sidecar is only necessary for the final release and shipping, Tauri requires it to be present during development as well.

## Build AI Studio from source
In order to build MindWork AI Studio from source instead of using the pre-built binaries, follow these steps:
1. Ensure you have met all the prerequisites.
2. Open a terminal.
3. Navigate to the `/app/Build` directory within the repository.
4. To build the current version, run `dotnet run build` to build the entire app.
    - This will build the app for the current operating system and CPU architecture (x64, or ARM64).
    - The final setup program will be located in `runtime/target/release` afterward.

## Run the app locally with all your changes
Do you want to test your changes before creating a PR? Follow these steps:
1. Ensure you have met all the prerequisites.
2. At least once, you have to run the `dotnet run build` command (see above, "One-time mandatory steps"). This is necessary because the Tauri framework checks whether the .NET app as so-called "sidecar" is available. Although the sidecar is only necessary for the final release and shipping, Tauri requires it to be present during development.
3. Open a terminal.
4. Navigate to the `runtime` directory within the repository, e.g. `cd repos/mindwork-ai-studio/runtime`.
5. Run `cargo tauri dev --no-watch`.
6. Now you have to wait until the Rust code is compiled. Afterward, the Tauri runtime will be started and the app window will appear. This may take a while, especially the first time you run it.
7. Open another terminal or tab.
8. Navigate to the `app/MindWork AI Studio` directory within the repository.
9. Execute the command `dotnet run`.
10. After compiling the .NET code, the app will finally start inside the Tauri runtime window.

You can now test your changes.

## Create a release
In order to create a release:
1. To create a new release, you need to be a maintainer of the repository—see step 8.
2. Make sure there's a changelog file for the version you want to create in the `/app/MindWork AI Studio/wwwroot/changelog` directory. Name the file `vX.Y.Z.md` and include all release changes—your updates and any others included in this version.
3. After you have created the changelog file, you must commit the changes to the repository.
4. To prepare a new release, open a terminal, go to `/app/Build` and run `dotnet run release --action <ACTION>`, where `<ACTION>` is either `patch` (creating a patch version), `minor` (creating a minor version), or `major` (creating a major version).
5. Now wait until all process steps have been completed. Among other things, the version number will be incremented, the new changelog registered, and the version numbers of central dependencies updated, etc.
6. The actual release will be built by our GitHub Workflow. For this to work, you need to create a PR with your changes.
7. Your proposed changes will be reviewed and merged.
8. Once the PR is merged, a member of the maintainers team will create & push an appropriate git tag in the format `vX.Y.Z`.
9. The GitHub Workflow will then build the release and upload it to the [release page](https://github.com/MindWorkAI/AI-Studio/releases/latest).
10. Building the release including virus scanning takes some time. Please be patient.