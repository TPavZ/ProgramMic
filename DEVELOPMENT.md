# ProgramMic v2 development

Branch: `dev/v2`. Version: `2.0.0-dev`.

The first UI pass preserves the dark gray and pink palette, with separate app and microphone cards and a final mix destination. HTML, CSS and JavaScript live in `UI/` and are copied into build and publish output. `WebUi.cs` connects the local WebView2 page to the existing C# audio controls. The classic interface remains the fallback when WebView2 cannot initialize.

## First additions

- Runs in the system tray when closed or minimized; double-click to reopen, or choose Exit ProgramMic to quit. Routing and hotkeys keep running while hidden.
- Starts at Windows sign-in in tray mode through the current user's Run entry. Windows Task Manager's Startup apps can disable startup. Launching another copy reopens the existing instance. `ProgramMic.exe --exit` requests a graceful exit for development rebuilds.

- Microphone mute/unmute without changing saved microphone volume. Mute resets when the app closes.
- Click Assign hotkey, then press the next non-modifier key, matching v1. The previous key is restored if Windows rejects the selection. Switching away cancels assignment.
- Clear routing status, responsive layout and keyboard focus indicators.
- Fix device selection restart: the previous restart path returned early while holding its own restart flag.

## Build and run

Run `Start-Live.cmd` for a local development window. It sets `PROGRAMMIC_DEV_UI` to the source `UI/` folder. Saved HTML, CSS and JavaScript edits reload after a short debounce without restarting the C# mixer; current state is resent after navigation. The window title shows the last detected update. Normal launches use the bundled UI without watching files. C# changes require closing the development window and running the launcher again.

Requires the .NET 9 SDK, Windows 10 build 19041 or newer, Microsoft Edge WebView2 Runtime, and VB-CABLE for audio routing.

```powershell
dotnet build ProgramMic.csproj -c Release
dotnet run --project ProgramMic.csproj
dotnet publish ProgramMic.csproj -c Release -r win-x64 --self-contained true -o publish
```

The installer script consumes `publish/`. It does not yet install or verify WebView2 Runtime; resolve that before a public v2 release. Do not publish this development installer as v1.0.0. Compiled files stay out of Git.

## Validation and remaining work

The C# development build is checked at this milestone. Live audio routing, hotkey registration, WebView2 rendering and driver/device changes still require Windows end-to-end testing with VB-CABLE. The HTML can also be opened in a browser as a disabled design preview.

Next: review the layout, agree on additional v2 features, test mute and device switching with real audio, then finish runtime detection and installer validation. The v1 README and release remain preserved.

## Continuous microphone routing
Microphone-to-VB-CABLE routing starts independently of the selected application. Activate adds application audio; Deactivate only mutes that application contribution. Microphone mute remains independent. Switching application sources or losing their capture does not stop the microphone. Changing audio devices still requires restarting the device streams. Real-device listening verification remains required.

## Soundboard
The Soundboard button opens a sliding right-hand panel. Add local audio clips, rename them, adjust individual or overall soundboard volume, play clips, and Stop All. The library copies imported files into the user's AppData/ProgramMic/Soundboard folder and saves names and volumes there. Clips are limited to 50 MB and two minutes; up to eight different clips can overlap, and replaying a clip restarts it.

Soundboard audio joins the same selected output as the microphone. It plays with application routing active or inactive, and microphone mute does not mute soundboard clips. FinalMix volume applies to all audio. Mixer checks cover independent routing, microphone mute, master volume, Stop All, and clipping. Listening through VB-CABLE remains an end-to-end verification step.

The soundboard now uses a three-column controller pad grid, starting with 15 pads. Click an empty pad to assign a local clip; click an assigned pad to play it. The ellipsis on an assigned pad opens its name, Change Sound, Remove, and volume controls. Pad positions persist, and existing libraries migrate into sequential pads. Removing a sound leaves its pad available for reassignment. More rows appear as the library grows.
