# ProgramMic

**Route application audio and your microphone through a single virtual microphone.**

ProgramMic is a lightweight Windows audio-routing utility for Discord, games, voice chat, and similar applications. Select a running application's audio, mix it with your microphone, and send the combined audio through a virtual microphone destination.

## Download

### TBD

Windows 10/11 x64 · Self-contained installer · VB-CABLE required

[View the v1.0.0 release notes](https://github.com/TPavZ/ProgramMic/releases/tag/v1.0.0)

## Version

Current release: **v1.0.0**

## Features

- Select audio from a running application
- Mix program audio with your physical microphone
- Send the combined mix through VB-CABLE
- Independent Program, Microphone, and Virtual Mic volume controls
- Global hotkey to toggle Program Audio on/off
- Assign a custom hotkey by pressing the desired key
- Remembers your last devices, application, volume levels, and hotkey
- Compact dark-mode interface
- Self-contained Windows installation — no separate .NET installation required

## Installation

1. Download.
2. Run the installer.
3. ProgramMic requires **VB-CABLE** from VB-Audio. Setup automatically checks whether it is installed.
4. If VB-CABLE is missing, click **Download VB-CABLE** in Setup, install the driver, and restart Windows if requested.
5. Return to ProgramMic Setup and click **Recheck**.
6. Once VB-CABLE is detected, continue the installation and launch ProgramMic.

Official VB-CABLE download: https://vb-audio.com/Cable/

## Basic Setup

1. Under **Program Audio**, choose the application whose sound you want to send.
2. Under **Your Microphone**, choose your physical microphone.
3. Under **Virtual Mic Destination**, choose the VB-CABLE destination.
4. Adjust the three volume controls as needed.
5. Use the configured hotkey (**F8 by default**) to toggle Program Audio on or off.
6. In Discord, your game, or voice-chat application, select the corresponding VB-CABLE recording/input device as your microphone.

### Discord

For the most accurate routed audio, disable Discord **Noise Suppression** if it removes or distorts program audio being sent through ProgramMic.

## Updating

Future ProgramMic installers are designed to upgrade the existing installation. User preferences are stored separately in AppData so normal upgrades can preserve saved selections, volume levels, and the assigned hotkey.

## Requirements

- Windows 10/11 x64
- VB-Audio VB-CABLE

## Contact

Created by **@tpavz**  
Discord: https://discord.com/users/355793811827458049

## Third-Party Software

VB-CABLE is separate software from VB-Audio Software. ProgramMic does not bundle VB-CABLE; Setup links users to VB-Audio's official download page.
