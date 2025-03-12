# Building
You just want to use the app? Then simply [download the appropriate setup for your operating system](Setup.md). This chapter is intended for developers who want to modify and customize the code.

## Prerequisites
1. Install the [.NET 9 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0).
2. [Install the Rust compiler](https://www.rust-lang.org/tools/install) in the latest version.
3. Met the prerequisites for building [Tauri](https://tauri.app/v1/guides/getting-started/prerequisites/). Node.js is **not** required, though.
4. Install the Tauri CLI by running `cargo install --version 1.6.2 tauri-cli`.
5. [Install NuShell](https://www.nushell.sh/). NuShell works on all operating systems and is required because the build script is written in NuShell.
6. Clone the repository.

## One-time mandatory steps
Regardless of whether you want to build the app locally for yourself (not trusting the pre-built binaries) or test your changes before creating a PR, you have to run the following commands at least once:

1. Open a terminal using NuShell.
2. Navigate to the `/app/MindWork AI Studio` directory within the repository.
3. Run `dotnet restore` to bring up the .NET dependencies.
4. Run `nu build.nu publish` to build the entire app.

This is necessary because the build script and the Tauri framework assume that the .NET app is available as a so-called "sidecar." Although the sidecar is only necessary for the final release and shipping, Tauri requires it to be present during development as well.

## Build AI Studio from source
In order to build MindWork AI Studio from source instead of using the pre-built binaries, follow these steps:
1. Ensure you have met all the prerequisites.
2. Open a terminal with NuShell.
3. Navigate to the `/app/MindWork AI Studio` directory within the repository.
4. To build the current version, run `nu build.nu publish` to build the entire app.
    - This will build the app for the current operating system, for both x64 (Intel, AMD) and ARM64 (e.g., Apple Silicon, Raspberry Pi).
    - The final setup program will be located in `runtime/target/release/bundle` afterward.
5. In order to create a new release:
   1. Before finishing the PR, make sure to create a changelog file in the `/app/MindWork AI Studio/wwwroot/changelog` directory. The file should be named `vX.Y.Z.md` and contain the changes made in the release (your changes and any other changes that are part of the release).
   2. To prepare a new release, run `nu build.nu prepare <ACTION>`, where `<ACTION>` is either `patch`, `minor`, or `major`.
   3. The actual release will be built by our GitHub Workflow. For this to work, you need to create a PR with your changes.
   4. Your proposed changes will be reviewed and merged.
   5. Once the PR is merged, a member of the maintainers team will create & push an appropriate git tag in the format `vX.Y.Z`.
   6. The GitHub Workflow will then build the release and upload it to the [release page](https://github.com/MindWorkAI/AI-Studio/releases/latest).
   7. Building the release including virus scanning takes some time. Please be patient.

## Run the app locally with all your changes
Do you want to test your changes before creating a PR? Follow these steps:
1. Ensure you have met all the prerequisites.
2. At least once, you have to run the `nu build.nu publish` command (see above, "Build instructions"). This is necessary because the Tauri framework checks whether the .NET app as so-called "sidecar" is available. Although the sidecar is only necessary for the final release and shipping, Tauri requires it to be present during development.
3. Open a terminal (in this case, it doesn't have to be NuShell).
4. Navigate to the `runtime` directory within the repository, e.g. `cd repos/mindwork-ai-studio/runtime`.
5. Run `cargo run`.

Cargo will compile the Rust code and start the runtime. The runtime will then start the .NET compiler. When the .NET source code is compiled, the app will start. You can now test your changes.