# Install the app
To get started, choose your operating system where you want to install the app:

- [Windows](#windows)
- [macOS](#macos)
- [Linux](#linux)

## Windows
AI Studio is only available for modern 64-bit Windows systems. When you have an older 32-bit system, you won't be able to run the app. We require an updated Windows 10 or 11 version; the app won't properly work on Windows 7 or 8. Next, we have to figure out if you have an Intel/AMD or a modern ARM system on your Windows machine.

- **Copilot Plus PC:** Do you own one of the new Copilot Plus PCs? If so, you most likely have an ARM system. When your machine has a Qualcomm sticker on it, you have an ARM system for sure. [Download the ARM version](https://github.com/MindWorkAI/AI-Studio/releases/latest/download/MindWork%20AI%20Studio_arm64-setup.exe) of AI Studio.
 
- **Windows on macOS:** Do you run Windows using Parallels on an Apple Silicon system (M1, M2, M3, M4)? Then you have an ARM system as well. [Download the ARM version](https://github.com/MindWorkAI/AI-Studio/releases/latest/download/MindWork%20AI%20Studio_arm64-setup.exe) of AI Studio.

- **Intel/AMD:** In almost all other cases, you have an Intel/AMD system. [Download the x64 version](https://github.com/MindWorkAI/AI-Studio/releases/latest/download/MindWork%20AI%20Studio_x64-setup.exe) of AI Studio.

When you try to install the app, you get a message regarding protection of your PC (see screenshots below). For Windows to trust our app, we need to purchase a certificate that costs around $1000 per year. Would you like to help us with this? [Please consider supporting us](../Sponsors.md). You might want to [visit our release page](https://github.com/MindWorkAI/AI-Studio/releases/latest). There, we provide VirusTotal scan results for each release. If you are unsure about the safety of the app, you can check the results there. When you are confident in the app's safety, click on "More info" and then "Run anyway" to proceed with the installation:

![Windows Protection 1](Windows%20Warning%201.png)

![Windows Protection 2](Windows%20Warning%202.png)

## macOS
AI Studio is available for modern 64-bit macOS systems. The minimum requirement is macOS 10.13 (High Sierra). Next, we have to figure out if you have an Intel or a modern Apple Silicon (ARM) system.

- **Apple Silicon:** Do you have a modern Apple Silicon system (M1, M2, M3, M4)?  [Download the ARM version](https://github.com/MindWorkAI/AI-Studio/releases/latest/download/MindWork%20AI%20Studio_aarch64.dmg) of AI Studio.

- **Time of purchase:** Did you buy your Mac in 2021 or later? Then you probably have an ARM system. [Download the ARM version](https://github.com/MindWorkAI/AI-Studio/releases/latest/download/MindWork%20AI%20Studio_aarch64.dmg) of AI Studio.

- **Check System Information:** On your macOS, click on the Apple logo in the top left corner, then "About This Mac." In the window that opens, you can see the processor type. When it says "Apple M1" or similar, you have an ARM system. [Download the ARM version](https://github.com/MindWorkAI/AI-Studio/releases/latest/download/MindWork%20AI%20Studio_aarch64.dmg) of AI Studio.

- **Intel:** Older Macs have an Intel processor. [Download the Intel version](https://github.com/MindWorkAI/AI-Studio/releases/latest/download/MindWork%20AI%20Studio_x64.dmg) of AI Studio.

After downloading the file, open the DMG file and drag the app to your Applications folder:

![macOS Installation 1](macOS%20Mount.png)

When you try to open the app, you get a message that the app is damaged:

![macOS Installation 2](macOS%20Damage.png)

This is because we don't have an Apple Developer account, which costs around $100 per year. Would you like to help us with this? [Please consider supporting us](../Sponsors.md). You might want to [visit our release page](https://github.com/MindWorkAI/AI-Studio/releases/latest). There, we provide VirusTotal scan results for each release. If you are unsure about the safety of the app, you can check the results there. When you are confident in the app's safety, follow these steps:

1. Start the Terminal app. Just use Spotlight (Cmd + Space) and type "Terminal."
2. Open your Finder and navigate to the Applications folder.
3. Find the MindWork AI Studio app.
4. Type this command: `xattr -r -d com.apple.quarantine ` (with a space at the end).
5. Drag the MindWork AI Studio app from the Finder into the Terminal window. The path to the app will be added to the command automatically.
6. The final command should be: `xattr -r -d com.apple.quarantine /Applications/MindWork\ AI\ Studio.app`. Press Enter.
7. Now, you might close the Terminal app and the Finder.

The AI Studio app should now open without any issues.

## Linux

mind-work-ai-studio_amd64.AppImage
mind-work-ai-studio_amd64.deb