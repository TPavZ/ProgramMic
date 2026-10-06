# ProgramMic v2 development

Branch: `dev/v2`. Version: `2.0.0-dev`.

The first UI pass preserves the dark gray and pink palette, with separate app and microphone cards and a final mix destination. HTML, CSS and JavaScript live in `UI/` and are copied into build and publish output. `WebUi.cs` connects the local WebView2 page to the existing C# audio controls. The classic interface remains the fallback when WebView2 cannot initialize.

## First additions

- Microphone mute/unmute without changing saved microphone volume. Mute resets when the app closes.
- Function-key hotkey selection (F1–F24), with rollback if Windows cannot register a key.
- Clear routing status, responsive layout and keyboard focus indicators.
- Fix device selection restart: the previous restart path returned early while holding its own restart flag.

## Build and run

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
