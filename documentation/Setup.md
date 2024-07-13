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

When you try to install the app, you get a message regarding protection of your PC (see screenshots below). For Windows to trust our app, we need to purchase a certificate that costs around $1000 per year. Would you like to help us with this? [Please consider supporting us](https://github.com/sponsors/MindWorkAI). You might want to [visit our release page](https://github.com/MindWorkAI/AI-Studio/releases/latest). There, we provide VirusTotal scan results for each release. If you are unsure about the safety of the app, you can check the results there. Ensure that the majority of scanners have a green checkmark.

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

This is because we don't have an Apple Developer account, which costs around $100 per year. Would you like to help us with this? [Please consider supporting us](https://github.com/sponsors/MindWorkAI). You might want to [visit our release page](https://github.com/MindWorkAI/AI-Studio/releases/latest). There, we provide VirusTotal scan results for each release. If you are unsure about the safety of the app, you can check the results there. Ensure that the majority of scanners have a green checkmark.

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
AI Studio is available for modern 64-bit Linux systems. The app is provided as an AppImage and a DEB package. We test our app using Ubuntu 22.04, but it should work on other distributions as well.

1. **Choosing between the AppImage and DEB package:**
   - **AppImage:** This is a single file that you can run without installation. It is a bit larger than the DEB package. Main advantage is that automatic updates are supported. When a new version is available, the app will prompt you to download and install it. Unfortunately, the AppImage version is not yet supported for ARM systems.

   - **DEB package:** This is a traditional Debian package that you can install using your package manager. It is smaller than the AppImage. The main disadvantage is that you have to check for updates manually. Unfortunately, we don't have a Debian repository for automatic updates yet. That means:
     - You won't get a notification when a new version is available.
     - You have to download any updated DEB package from our [release page](https://github.com/MindWorkAI/AI-Studio/releases/latest) and install it manually again.

    Another issue is that the DEB package is only compatible with Debian-based distributions. For other distributions, you have to use the AppImage version.

2. **Choosing CPU architecture:**
    Next, we have to figure out if you have an Intel/AMD or a modern ARM system on your Linux machine. Open a terminal and run the command `uname -m`. When the output is `x86_64`, you have an Intel/AMD system. If the output is `aarch64`, you have an ARM system.

   - **Intel/AMD:** [Download the AppImage](https://github.com/MindWorkAI/AI-Studio/releases/latest/download/mind-work-ai-studio_amd64.AppImage) (recommended) or [DEB package](https://github.com/MindWorkAI/AI-Studio/releases/latest/download/mind-work-ai-studio_amd64.deb) of AI Studio.

   - **ARM:** Unfortunately, the AppImage version is not yet supported for ARM systems. [Download the DEB package](https://github.com/MindWorkAI/AI-Studio/releases/latest/download/mind-work-ai-studio_arm64.deb) of AI Studio instead.

### DEB Package Installation

**Install the app using the desktop environment:**
1. Download the DEB package from the link above.
2. Open your file manager and navigate to the Downloads folder.
3. Right-click on the DEB package and select "Open with other application":
   ![Linux Installation 1](Ubuntu%20DEB%20Open.png)
4. Choose your package manager, e.g., "Software Install":
   ![Linux Installation 2](Ubuntu%20DEB%20Install%201.png)
5. Click on "Install":
   ![Linux Installation 3](Ubuntu%20DEB%20Install%202.png)
6. Enter your password and click on "Authenticate."
7. Wait for the installation to finish.
8. Close the package manager.
9. You can now find the app in your application menu.

**Install the app using the terminal:**
1. Download the DEB package from the link above.
2. Open a terminal and navigate to the Downloads folder: `cd Downloads`.
3. Install the DEB package: `sudo apt install ./mind-work-ai-studio_amd64.deb`.
4. Enter your password and press Enter.
5. Wait for the installation to finish.
6. You can now find the app in your application menu.

### AppImage Installation

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
2. Open a terminal and navigate to the Downloads folder: `cd Downloads`.
3. Make the AppImage executable: `chmod +x mind-work-ai-studio_amd64.AppImage`.
4. You might want to move the AppImage to a more convenient location, e.g., your home directory: `mv mind-work-ai-studio_amd64.AppImage ~/`.
4. Now you can run the AppImage from your file manager (double-click) or the terminal: `./mind-work-ai-studio_amd64.AppImage`.