# Building
You just want to use the app? Then simply [download the appropriate setup for your operating system](Setup.md). This chapter is intended for developers who want to modify and customize the code.

In order to build MindWork AI Studio from source instead of using the pre-built binaries, follow these steps:
1. Install the [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).
2. [Install the Rust compiler](https://www.rust-lang.org/tools/install) in the latest version.
3. [Install NuShell](https://www.nushell.sh/). This shell works on all operating systems and is required because the build script is written in NuShell.
4. Clone the repository.
5. Open a terminal with NuShell.
6. Navigate to the `/app/MindWork AI Studio` directory within the repository.
7. To build the current version, run `nu build.nu publish`.
    - This will build the app for the current operating system, for both x64 (Intel, AMD) and ARM64 (e.g., Apple Silicon, Raspberry Pi).
    - The setup program will be located in `runtime/target/release/bundle` afterward.
8. In order to create a new release:
   1. To prepare a new release, run `nu build.nu prepare <ACTION>`, where `<ACTION>` is either `patch`, `minor`, or `major`. In case you need to adjust the version manually, you can do so in the `metadata.txt` file at the root of the repository; the first line contains the version number.
   2. The actual release will be built by our GitHub Workflow. For this to work, you need to create a PR with your changes.
   3. Before finishing the PR, make sure to create a changelog file in the `/app/MindWork AI Studio/wwwroot/changelog` directory. The file should be named `vX.Y.Z.md` and contain the changes made in the release (your changes and any other changes that are part of the release).
   4. Your proposed changes will be reviewed and merged.
   5. Once the PR is merged, a member of the maintainers team will create & push an appropriate git tag in the format `vX.Y.Z`.
   6. The GitHub Workflow will then build the release and upload it to the [release page](https://github.com/MindWorkAI/AI-Studio/releases/latest).
   7. Building the release including virus scanning takes about 2 1/2 hours.
