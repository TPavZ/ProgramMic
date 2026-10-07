using System.Text.Json;

namespace ProgramMic;

public sealed partial class MainForm
{
    private readonly Dictionary<int, string> registeredSoundHotkeys = new();
    private Keys pendingSoundHotkey;
    private string? pendingSoundEditId;
    private bool assigningSoundHotkey;

    private bool SoundHotkeyInUse(Keys key, string? exceptId = null) => key != Keys.None &&
        soundboardSettings.Clips.Any(c => c.Id != exceptId && c.Hotkey == (int)key);

    private void UnregisterSoundHotkeys()
    {
        foreach (var id in registeredSoundHotkeys.Keys) UnregisterHotKey(Handle, id);
        registeredSoundHotkeys.Clear();
    }

    private void RegisterSoundHotkeys(bool showErrors = false)
    {
        UnregisterSoundHotkeys();
        var used = new HashSet<Keys> { hotkeyKey, micHotkeyKey, Keys.None };
        bool repaired = false;
        var failed = new List<string>();
        foreach (var clip in soundboardSettings.Clips)
        {
            var key = (Keys)clip.Hotkey;
            if (key == Keys.None) continue;
            if (!IsSoundHotkey(key) || !used.Add(key)) { clip.Hotkey = 0; repaired = true; continue; }
            int id = 0x6000 + soundboardSettings.Clips.IndexOf(clip);
            if (RegisterHotKey(Handle, id, (uint)Mods.NoRepeat, (uint)key)) registeredSoundHotkeys[id] = clip.Id;
            else failed.Add($"{clip.Name}: {key}");
        }
        if (repaired) SaveSoundboard();
        if (showErrors && failed.Count > 0)
            MessageBox.Show(this, "Windows could not register these sound hotkeys. They may be used by another app:\n\n" + string.Join("\n", failed), "Sound Hotkeys");
    }

    private static bool IsSoundHotkey(Keys key) => Enum.IsDefined(typeof(Keys), key) &&
        key is not (Keys.None or Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin);

    private void ReleaseAllHotkeys()
    {
        UnregisterHotKey(Handle, HOTKEY_ID); UnregisterHotKey(Handle, MIC_HOTKEY_ID); UnregisterSoundHotkeys();
    }

    private void RestoreAllHotkeys()
    {
        if (closing || !IsHandleCreated) return;
        ReleaseAllHotkeys(); RegisterCurrentHotkey(false); RegisterMicHotkey(false); RegisterSoundHotkeys();
    }

    private void ValidateSoundHotkey(Keys key)
    {
        if (key == Keys.None) return;
        if (!IsSoundHotkey(key)) throw new InvalidOperationException("Choose a non-modifier key.");
        if (key == hotkeyKey || key == micHotkeyKey || SoundHotkeyInUse(key, pendingSoundEditId))
            throw new InvalidOperationException($"{key} is already assigned. Every sound, ProgramMic toggle, and microphone toggle must use a different key.");
        var existing = soundboardSettings.Clips.FirstOrDefault(c => c.Id == pendingSoundEditId);
        if (existing?.Hotkey == (int)key && registeredSoundHotkeys.ContainsValue(existing.Id)) return;
        const int checkId = 0x5FFF;
        if (!RegisterHotKey(Handle, checkId, (uint)Mods.NoRepeat, (uint)key))
            throw new InvalidOperationException($"Windows could not register {key}. Another app may already use it. Your previous assignment has been kept.");
        UnregisterHotKey(Handle, checkId);
    }

    private void BeginAssignSoundHotkey()
    {
        CancelHotkeyAssignment(); ReleaseAllHotkeys(); assigningSoundHotkey = true;
        ActiveControl = null; Focus();
    }

    private void CompleteSoundHotkeySelection(Keys key)
    {
        if (!assigningSoundHotkey || !IsSoundHotkey(key)) return;
        assigningSoundHotkey = false; RestoreAllHotkeys();
        try { ValidateSoundHotkey(key); pendingSoundHotkey = key; }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Hotkey Already Assigned", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        PublishWebState();
    }

    private async void PlaySoundFromHotkey(string id)
    {
        if (closing || webBusy || assigningHotkey || assigningMicHotkey || assigningSoundHotkey) return;
        try
        {
            var command = JsonSerializer.SerializeToElement(new { id });
            await HandleSoundboardCommandAsync(command, "soundPlay");
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Sound Playback"); }
    }
}
