using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Text.Json;

namespace ProgramMic;

public sealed partial class MainForm
{
    private WebView2? webUi;
    private bool webBusy, micMuted;
    private const int MIC_HOTKEY_ID = 0x504E;
    private Keys micHotkeyKey = Keys.None;
    private bool assigningMicHotkey;
    private int soundboardAddedWidth;
    private bool initialWebWindowSized;

    private async Task FitInitialWebWindowAsync()
    {
        if (initialWebWindowSized || closing || webUi?.CoreWebView2 is null) return;
        initialWebWindowSized = true;
        var measurements = await webUi.CoreWebView2.ExecuteScriptAsync(
            "[Math.ceil(document.querySelector('main').getBoundingClientRect().height),window.innerHeight]");
        if (closing || webUi.IsDisposed) return;
        var values = JsonSerializer.Deserialize<double[]>(measurements);
        if (values is not { Length: 2 } || values[1] <= 0) return;
        int frameHeight = Height - ClientSize.Height;
        int requiredHeight = (int)Math.Ceiling((values[0] + 8) * webUi.ClientSize.Height / values[1]);
        var area = Screen.FromControl(this).WorkingArea;
        ClientSize = new Size(ClientSize.Width, Math.Min(requiredHeight, area.Height - frameHeight));
        if (Bottom > area.Bottom) Top = Math.Max(area.Top, area.Bottom - Height);
    }

    private void SetSoundboardExpanded(bool open)
    {
        if (open && soundboardAddedWidth == 0 && WindowState == FormWindowState.Normal)
        {
            var area = Screen.FromControl(this).WorkingArea;
            int extra = Math.Min(360, Math.Max(0, area.Width - Width));
            soundboardAddedWidth = extra;
            Width += extra;
            if (Right > area.Right) Left = area.Right - Width;
        }
        else if (!open && soundboardAddedWidth > 0)
        {
            if (WindowState == FormWindowState.Normal) Width -= soundboardAddedWidth;
            soundboardAddedWidth = 0;
        }
    }

    private void ShowDuplicateHotkeyError(Keys key)
    {
        MessageBox.Show(this,
            $"{key} is already assigned to the other action.\n\nToggle ProgramMic and Toggle Microphone must use different keys. Your previous assignment has been kept.",
            "Hotkey Already Assigned", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private void ValidateLoadedHotkeys()
    {
        if (micHotkeyKey == Keys.None || micHotkeyKey != hotkeyKey) return;
        micHotkeyKey = Keys.None;
        SaveSettings();
        MessageBox.Show(this, "Your saved hotkeys used the same key. The microphone hotkey has been cleared. Please assign a different key for Toggle Microphone.",
            "Duplicate Saved Hotkeys", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private bool RegisterMicHotkey(bool showError)
    {
        if (micHotkeyKey == Keys.None) return true;
        if (!IsHandleCreated) return false;
        bool ok = RegisterHotKey(Handle, MIC_HOTKEY_ID, (uint)Mods.NoRepeat, (uint)micHotkeyKey);
        if (!ok && showError) MessageBox.Show($"Windows could not register {micHotkeyKey}. Another action or app may already use it.", "Microphone Hotkey");
        return ok;
    }

    private void CancelHotkeyAssignment()
    {
        if (assigningHotkey)
        {
            assigningHotkey = false;
            RegisterCurrentHotkey(false);
            hotkeyLabel.Text = HotkeyText();
            hotkeyLabel.ForeColor = TextSecondary;
            setHotkeyButton.Text = "ASSIGN HOTKEY";
            toggleButton.Enabled = true;
        }
        if (assigningMicHotkey) { assigningMicHotkey = false; RegisterMicHotkey(false); }
    }

    private void BeginAssignMicHotkey()
    {
        CancelHotkeyAssignment();
        UnregisterHotKey(Handle, MIC_HOTKEY_ID);
        assigningMicHotkey = true;
        ActiveControl = null;
        Focus();
    }

    private void CompleteMicHotkeySelection(Keys key)
    {
        if (!assigningMicHotkey || key is Keys.None or Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin) return;
        if (key == hotkeyKey)
        {
            CancelHotkeyAssignment();
            ShowDuplicateHotkeyError(key);
            PublishWebState();
            return;
        }
        var oldKey = micHotkeyKey;
        micHotkeyKey = key;
        assigningMicHotkey = false;
        if (!RegisterMicHotkey(true)) { micHotkeyKey = oldKey; RegisterMicHotkey(false); }
        SaveSettings(); PublishWebState();
    }

    private void ToggleMicrophone() { micMuted = !micMuted; UpdateGains(); PublishWebState(); }
    private readonly System.Windows.Forms.Timer webStateTimer = new() { Interval = 250 };
    private string? devUiFolder;
    private string devUiStamp = "";
    private DateTime devUiChangedAt;
    private bool devUiReloadPending;

    private string GetUiStamp() => string.Join("|", Directory.EnumerateFiles(devUiFolder!, "*", SearchOption.AllDirectories)
        .Where(p => Path.GetExtension(p) is ".css" or ".html" or ".js")
        .OrderBy(p => p).Select(p => p + File.GetLastWriteTimeUtc(p).Ticks + new FileInfo(p).Length.ToString()));

    private void RefreshDevUi()
    {
        if (devUiFolder is null || closing) return;
        try
        {
            var stamp = GetUiStamp();
            if (stamp != devUiStamp) { devUiStamp = stamp; devUiChangedAt = DateTime.UtcNow; devUiReloadPending = true; }
            if (devUiReloadPending && DateTime.UtcNow - devUiChangedAt > TimeSpan.FromMilliseconds(500))
            {
                devUiReloadPending = false;
                webUi?.CoreWebView2.Reload();
                Text = "ProgramMic v2 — Live development — UI updated " + DateTime.Now.ToLongTimeString();
            }
        }
        catch (IOException) { /* Editors may briefly lock files while saving. Retry next tick. */ }
        catch (UnauthorizedAccessException) { }
    }

    private async Task InitializeWebUiAsync()
    {
        webUi = new WebView2 { Dock = DockStyle.Fill, DefaultBackgroundColor = Bg };
        try
        {
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder:
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ProgramMic", "WebView2"));
            await webUi.EnsureCoreWebView2Async(environment);
            if (closing) return;
            var core = webUi.CoreWebView2;
            var requestedUi = Environment.GetEnvironmentVariable("PROGRAMMIC_DEV_UI");
            if (!string.IsNullOrWhiteSpace(requestedUi) && File.Exists(Path.Combine(requestedUi, "index.html")))
            {
                devUiFolder = Path.GetFullPath(requestedUi);
                devUiStamp = GetUiStamp();
                Text = "ProgramMic v2 — Live development";
            }
            core.SetVirtualHostNameToFolderMapping("programmic.local", devUiFolder ?? Path.Combine(AppContext.BaseDirectory, "UI"), CoreWebView2HostResourceAccessKind.DenyCors);
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            core.NewWindowRequested += (_, e) => e.Handled = true;
            core.NavigationStarting += (_, e) => e.Cancel = e.Uri != "https://programmic.local/index.html";
            core.WebMessageReceived += HandleWebCommand;
            Deactivate += (_, _) =>
            {
                CancelHotkeyAssignment();
                PublishWebState();
            };
            core.NavigationCompleted += async (_, e) =>
            {
                if (!e.IsSuccess) return;
                PublishWebState();
                try { await FitInitialWebWindowAsync(); }
                catch (Exception) { /* Retain the default size if the page closes during measurement. */ }
            };
            foreach (Control control in Controls) control.Visible = false;
            Controls.Add(webUi);
            webUi.BringToFront();
            ClientSize = new Size(1000, 900);
            MinimumSize = new Size(720, 680);
            core.Navigate("https://programmic.local/index.html");
            webStateTimer.Tick += (_, _) => { PublishWebState(); RefreshDevUi(); };
            webStateTimer.Start();
            FormClosed += (_, _) => { webStateTimer.Stop(); webStateTimer.Dispose(); };
        }
        catch (Exception ex)
        {
            webUi.Dispose();
            webUi = null;
            MessageBox.Show("The new interface could not start. The classic interface is available.\n\nCheck that Microsoft Edge WebView2 Runtime is installed.\n\n" + ex.Message, "ProgramMic v2");
            foreach (Control control in Controls) control.Visible = true;
        }
    }

    private void PublishWebState()
    {
        if (closing || webUi?.CoreWebView2 is null) return;
        object[] Options(ComboBox box) => box.Items.Cast<object>().Select((item, index) => (object)new { value = index, label = item.ToString() }).ToArray();
        webUi.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new
        {
            type = "state", running, programEnabled, micMuted, assigningHotkey, busy = webBusy,
            status = statusLabel.Text, hotkey = HotkeyText(),
            assigningMicHotkey, micHotkey = micHotkeyKey == Keys.None ? "Not assigned" : micHotkeyKey.ToString(),
            soundClips = soundboardSettings.Clips.Select(c => new { id = c.Id, name = c.Name, volume = c.Volume, pad = c.Pad, color = c.Color }).ToArray(),
            soundVolume = soundboardSettings.Volume, soundPlaying = soundboard.Playing,
            soundPreviewPlaying = soundPreviewOutput?.PlaybackState == NAudio.Wave.PlaybackState.Playing,
            soundPreviewPosition = SoundPreviewPosition, soundPreviewEnd,
            processes = Options(processBox), microphones = Options(micBox), outputs = Options(outputBox),
            process = processBox.SelectedIndex, microphone = micBox.SelectedIndex, output = outputBox.SelectedIndex,
            programVolume = programVolume.Value, micVolume = micVolume.Value, masterVolume = masterVolume.Value
        }));
    }

    private async void HandleWebCommand(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (closing || e.Source != "https://programmic.local/index.html") return;
        if (webBusy)
        {
            try
            {
                using var pending = JsonDocument.Parse(e.WebMessageAsJson);
                if (pending.RootElement.GetProperty("command").GetString() == "soundStop")
                {
                    soundPlaybackEpoch++; soundboard.StopAll(); PublishWebState();
                }
            }
            catch (JsonException) { }
            return;
        }
        try
        {
            using var doc = JsonDocument.Parse(e.WebMessageAsJson);
            var root = doc.RootElement;
            var command = root.GetProperty("command").GetString();
            webBusy = true;
            if (command is "soundPreview" or "soundPreviewStop" or "soundBrowse" or "soundCancel" or "soundImport" or "soundPlay" or "soundStop" or "soundMaster" or "soundRename" or "soundVolume" or "soundRemove")
            {
                PublishWebState();
                await HandleSoundboardCommandAsync(root, command);
                return;
            }
            switch (command)
            {
                case "ready": SetSoundboardExpanded(false); break;
                case "soundPanel":
                    SetSoundboardExpanded(root.GetProperty("open").GetBoolean());
                    break;
                case "cancelHotkey":
                    CancelHotkeyAssignment();
                    break;
                case "toggle":
                    await EnsureEngineRunningAsync();
                    if (running) ToggleProgram();
                    break;
                case "refresh":
                    restartingEngine = true;
                    try { StopEngine(); LoadDevices(); LoadProcesses(); }
                    finally { restartingEngine = false; }
                    await EnsureEngineRunningAsync();
                    break;
                case "select":
                    var target = root.GetProperty("target").GetString();
                    ComboBox? box = target switch { "process" => processBox, "microphone" => micBox, "output" => outputBox, _ => null };
                    int index = root.GetProperty("value").GetInt32();
                    if (box is null || index < 0 || index >= box.Items.Count) break;
                    if (box == processBox)
                    {
                        restartingEngine = true;
                        try { box.SelectedIndex = index; }
                        finally { restartingEngine = false; }
                        await ChangeProgramSourceAsync();
                        break;
                    }
                    // Hold restart ownership while changing selection to avoid overlapping async event handlers.
                    restartingEngine = true;
                    try { StopEngine(); box.SelectedIndex = index; }
                    finally { restartingEngine = false; }
                    await EnsureEngineRunningAsync();
                    break;
                case "volume":
                    int value = root.GetProperty("value").GetInt32();
                    var slider = root.GetProperty("target").GetString() switch { "program" => programVolume, "microphone" => micVolume, "output" => masterVolume, _ => null };
                    if (slider is not null)
                    {
                        slider.Value = Math.Clamp(value, slider.Minimum, slider.Maximum);
                        if (slider == micVolume) { micMuted = false; UpdateGains(); }
                    }
                    break;
                case "muteMic":
                    ToggleMicrophone();
                    break;
                case "assignMicHotkey":
                    BeginAssignMicHotkey();
                    break;
                case "assignHotkey":
                    CancelHotkeyAssignment();
                    BeginAssignHotkey();
                    break;
            }
        }
        catch (Exception ex) { MessageBox.Show("Could not apply that change.\n\n" + ex.Message, "ProgramMic"); }
        finally { webBusy = false; PublishWebState(); }
    }

    private void CompleteHotkeySelection(Keys key)
    {
        if (!assigningHotkey || key is Keys.None or Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin) return;
        if (key == micHotkeyKey)
        {
            CancelHotkeyAssignment();
            hotkeyLabel.Text = HotkeyText();
            setHotkeyButton.Text = "ASSIGN HOTKEY";
            toggleButton.Enabled = true;
            ShowDuplicateHotkeyError(key);
            PublishWebState();
            return;
        }
        var oldKey = hotkeyKey;
        var oldMods = hotkeyMods;
        hotkeyKey = key;
        hotkeyMods = Mods.None;
        assigningHotkey = false;
        if (!RegisterCurrentHotkey(true))
        {
            hotkeyKey = oldKey; hotkeyMods = oldMods; RegisterCurrentHotkey(false);
        }
        hotkeyLabel.Text = HotkeyText();
        hotkeyLabel.ForeColor = TextSecondary;
        setHotkeyButton.Text = "ASSIGN HOTKEY";
        toggleButton.Enabled = true;
        UpdateToggleUi(); SaveSettings(); PublishWebState();
    }
}
