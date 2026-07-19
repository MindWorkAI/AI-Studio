# Install the app
To get started, choose your operating system where you want to install the app:

- [Windows](#windows)
- [macOS](#macos)
- [Linux](#linux)

## Windows
AI Studio is only available for modern 64-bit Windows systems. When you have an older 32-bit system, you won't be able to run the app. We require an updated Windows 10 or 11 version; the app won't properly work on Windows 7 or 8. Next, we have to figure out if you have an Intel/AMD or a modern ARM system on your Windows machine.

- **Copilot+ PC:** Do you own one of the new Copilot+ PCs? If so, you most likely have an ARM system. When your machine has a Qualcomm sticker on it, you have an ARM system for sure. [Download the ARM version](https://github.com/MindWorkAI/AI-Studio/releases/latest/download/MindWork.AI.Studio_arm64-setup.exe) of AI Studio.
 
- **Windows on macOS:** Do you run Windows using Parallels on an Apple Silicon system (M1, M2, M3, M4)? Then you have an ARM system as well. [Download the ARM version](https://github.com/MindWorkAI/AI-Studio/releases/latest/download/MindWork.AI.Studio_arm64-setup.exe) of AI Studio.

- **Intel/AMD:** In almost all other cases, you have an Intel/AMD system. [Download the x64 version](https://github.com/MindWorkAI/AI-Studio/releases/latest/download/MindWork.AI.Studio_x64-setup.exe) of AI Studio.

When you try to install the app, you get a message regarding protection of your PC (see screenshots below). For Windows to trust our app, we need to purchase a certificate that [costs around $1000 per year](https://github.com/MindWorkAI/Planning/issues/56). Would you like to help us with this? [Please consider supporting us](https://github.com/sponsors/MindWorkAI). You might want to [visit our release page](https://github.com/MindWorkAI/AI-Studio/releases/latest). There, we provide VirusTotal scan results for each release. If you are unsure about the safety of the app, you can check the results there. Ensure that the majority of scanners have a green checkmark.

When you are confident in the app's safety, click on "More info" and then "Run anyway" to proceed with the installation:

![Windows Protection 1](Windows%20Warning%201.png)

![Windows Protection 2](Windows%20Warning%202.png)

Once the app is installed, it will check for updates automatically. If a new version is available, you will be prompted to install it.

## macOS
AI Studio is available for modern 64-bit macOS systems. The minimum requirement is macOS 10.13 (High Sierra). Next, we have to figure out if you have an Intel or a modern Apple Silicon (ARM) system.

- **Apple Silicon:** Do you have a modern Apple Silicon system (M1, M2, M3, M4)?  [Download the ARM version](https://github.com/MindWorkAI/AI-Studio/releases/latest/download/MindWork.AI.Studio_aarch64.dmg) of AI Studio.

- **Time of purchase:** Did you buy your Mac in 2021 or later? Then you probably have an ARM system. [Download the ARM version](https://github.com/MindWorkAI/AI-Studio/releases/latest/download/MindWork.AI.Studio_aarch64.dmg) of AI Studio.

- **Check System Information:** On your macOS, click on the Apple logo in the top left corner, then "About This Mac." In the window that opens, you can see the processor type. When it says "Apple M1" or similar, you have an ARM system. [Download the ARM version](https://github.com/MindWorkAI/AI-Studio/releases/latest/download/MindWork.AI.Studio_aarch64.dmg) of AI Studio.

- **Intel:** Older Macs have an Intel processor. [Download the Intel version](https://github.com/MindWorkAI/AI-Studio/releases/latest/download/MindWork.AI.Studio_x64.dmg) of AI Studio.

After downloading the file, open the DMG file and drag the app to your Applications folder:

![macOS Installation 1](macOS%20Mount.png)

When you try to open the app, you get a message that the app is damaged:

![macOS Installation 2](macOS%20Damage.png)

This is because we don't have an Apple Developer account, [which costs around $100 per year](https://github.com/MindWorkAI/Planning/issues/56). Would you like to help us with this? [Please consider supporting us](https://github.com/sponsors/MindWorkAI). You might want to [visit our release page](https://github.com/MindWorkAI/AI-Studio/releases/latest). There, we provide VirusTotal scan results for each release. If you are unsure about the safety of the app, you can check the results there. Ensure that the majority of scanners have a green checkmark.

When you are confident in the app's safety, follow these steps:

1. Start the Terminal app. Just use Spotlight (Cmd + Space) and type "Terminal."
2. Open your Finder and navigate to the Applications folder.
3. Find the MindWork AI Studio app.
4. Type this command: `xattr -r -d com.apple.quarantine ` (with a space at the end).
5. Drag the MindWork AI Studio app from the Finder into the Terminal window. The path to the app will be added to the command automatically.
6. The final command should be: `xattr -r -d com.apple.quarantine "/Applications/MindWork AI Studio.app"`. Press Enter.
7. Now, you might close the Terminal app and the Finder.

The AI Studio app should now open without any issues. Once the app is installed, it will check for updates automatically. If a new version is available, you will be prompted to install it.

## Linux
MindWork AI Studio is available for modern 64-bit Linux systems. Starting with release v26.7.3, Flatpak is the recommended installation method. We test AI Studio on Ubuntu 24.04 and 26.04, Kubuntu 24.04, Fedora 43 or newer, and openSUSE Leap 16 or newer, but it should work on other distributions as well.

First, determine whether your system uses the Intel/AMD or ARM architecture:

```bash
uname -m
```

`x86_64` means Intel/AMD; `aarch64` means ARM.

### Recommended: Flatpak Installation

On Ubuntu, install Flatpak first:

```bash
sudo apt update
sudo apt install flatpak
```

For other Linux distributions, follow the [official Flatpak setup instructions](https://flatpak.org/setup/).

Open the [latest AI Studio release](https://github.com/MindWorkAI/AI-Studio/releases/latest) and download the bundle for your architecture:

- **Intel/AMD (`x86_64`):** `MindWork.AI.Studio_x86_64.flatpak`
- **ARM (`aarch64`):** `MindWork.AI.Studio_aarch64.flatpak`

Install the downloaded bundle for your user account. For Intel/AMD, run:

```bash
cd ~/Downloads
flatpak install --user ./MindWork.AI.Studio_x86_64.flatpak
```

For ARM, run:

```bash
cd ~/Downloads
flatpak install --user ./MindWork.AI.Studio_aarch64.flatpak
```

Confirm the installation of the required GNOME runtime from Flathub when Flatpak asks for it.

#### Pandoc Extension (Strongly Recommended)

Pandoc is required for essential file features, including regular file attachments in chats, importing and converting Office documents, and other document-based functionality. We therefore strongly recommend installing the Pandoc extension. AI Studio checks whether a compatible Pandoc version is already available.

For Intel/AMD, download `MindWork.AI.Studio.Plugin.Pandoc_x86_64.flatpak` and run:

```bash
cd ~/Downloads
flatpak install --user ./MindWork.AI.Studio.Plugin.Pandoc_x86_64.flatpak
```

For ARM, download `MindWork.AI.Studio.Plugin.Pandoc_aarch64.flatpak` and run:

```bash
cd ~/Downloads
flatpak install --user ./MindWork.AI.Studio.Plugin.Pandoc_aarch64.flatpak
```

#### Starting and Updating the Flatpak

Start AI Studio from your application menu or run:

```bash
flatpak run org.MindWorkAI.AIStudio
```

If no application-menu entry appears, sign out of your desktop session completely and sign in again, or restart the system.

Until AI Studio is published on Flathub, bundles installed from GitHub do not receive automatic app updates. Download each new bundle and reinstall it. For Intel/AMD, run:

```bash
cd ~/Downloads
flatpak install --user --reinstall ./MindWork.AI.Studio_x86_64.flatpak
```

Use `MindWork.AI.Studio_aarch64.flatpak` instead on ARM.

### Alternative: AppImage Installation

If you prefer not to use Flatpak, AI Studio is also available as an AppImage:

- **Intel/AMD:** [Download the Intel/AMD AppImage](https://github.com/MindWorkAI/AI-Studio/releases/latest/download/mind-work-ai-studio_amd64.AppImage).
- **ARM:** [Download the ARM AppImage](https://github.com/MindWorkAI/AI-Studio/releases/latest/download/mind-work-ai-studio_aarch64.AppImage).

**Prepare the AppImage using the desktop environment:**
1. Download the AppImage from the link above.
2. Open your file manager and navigate to the Downloads folder.
3. Right-click on the AppImage and select "Properties":
   ![Ubuntu Installation 4](Ubuntu%20AppImage%20Properties.png)
4. Go to the "Permissions" tab and check the box "Allow executing file as program":
   ![Ubuntu Installation 5](Ubuntu%20AppImage%20Permissions.png)
5. Close the property window.
6. You might want to move the AppImage to a more convenient location, e.g., your home directory.
7. Double-click the AppImage to run it.

**Prepare the AppImage using the terminal:**
1. Download the AppImage from the link above.
2. Open a terminal and navigate to the Downloads folder: `cd ~/Downloads`.
3. Make the AppImage executable: `chmod +x mind-work-ai-studio_amd64.AppImage`.
4. You might want to move the AppImage to a more convenient location, e.g., your home directory: `mv mind-work-ai-studio_amd64.AppImage ~/`.
5. Now you can run the AppImage from your file manager (double-click) or the terminal: `~/mind-work-ai-studio_amd64.AppImage`.

Use the `aarch64` file name instead of the `amd64` file name on ARM systems.

### Secure Storage for API Keys

On Linux, AI Studio stores API keys through the FreeDesktop Secret Service API. A compatible password manager must provide this service, and it must have an unlocked default collection. AI Studio never creates, selects, unlocks, or changes a password manager's default collection itself.

Compatible configurations include:

- GNOME Keyring, which can be managed with an application such as Seahorse. Create a password collection if necessary, unlock it, and choose **Set as default**.
- KeePassXC with Secret Service integration enabled and a database group exposed to the service. Keep the relevant database and group unlocked when AI Studio needs to access secrets.

Automatic login can prevent GNOME Keyring from being unlocked automatically. If secure storage remains locked after login, unlock the default collection in your password manager.