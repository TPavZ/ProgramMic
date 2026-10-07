using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ProgramMic;

public sealed partial class MainForm
{
    private sealed class SoundClip
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "Sound";
        public string FileName { get; set; } = "";
        public int Volume { get; set; } = 100;
        public int Pad { get; set; } = -1;
        public string Color { get; set; } = "";
    }
    private sealed class SoundboardSettings
    {
        public int Volume { get; set; } = 100;
        public List<SoundClip> Clips { get; set; } = [];
    }
    private readonly SoundboardBus soundboard = new();
    private SoundboardSettings soundboardSettings = new();
    private readonly Dictionary<string, float[]> soundCache = new();
    private int soundPlaybackEpoch;
    private string? pendingSoundFile;
    private float[]? pendingSoundAudio;
    private WaveOut? soundPreviewOutput;
    private RawSourceWaveStream? soundPreviewStream;
    private double soundPreviewStart, soundPreviewEnd;

    private double SoundPreviewPosition => soundPreviewOutput is null ? -1 :
        Math.Clamp(soundPreviewStart + soundPreviewOutput.GetPosition() / 384000d, soundPreviewStart, soundPreviewEnd);

    private void StopSoundPreview()
    {
        soundPreviewOutput?.Stop();
        soundPreviewOutput?.Dispose(); soundPreviewOutput = null;
        soundPreviewStream?.Dispose(); soundPreviewStream = null;
    }

    private static float[] TrimSound(float[] audio, double start, double end)
    {
        double duration = audio.Length / 96000d;
        if (!double.IsFinite(start) || !double.IsFinite(end) || start < 0 || end > duration + .001 || end <= start)
            throw new InvalidOperationException("Choose an end time after the start time, within this clip.");
        int first = Math.Clamp((int)Math.Round(start * 48000), 0, audio.Length / 2) * 2;
        int last = Math.Clamp((int)Math.Round(end * 48000), 0, audio.Length / 2) * 2;
        if (last <= first) throw new InvalidOperationException("Select a longer portion of the clip.");
        return audio[first..last];
    }

    private static float[] TrimSoundForSave(float[] audio, double start, double end)
    {
        if (end - start > 35.0000001)
            throw new InvalidOperationException("Soundboard clips must be 35 seconds or less. Shorten your selection before saving.");
        return TrimSound(audio, start, end);
    }
    private static readonly string[] PadColors = ["#FAEDCB", "#C9E4DE", "#C6DEF1", "#DBCDF0", "#F2C6DE", "#F7D9C4"];
    private static string SoundboardFolder => Path.Combine(SettingsDirectory, "Soundboard");
    private static string SoundboardSettingsPath => Path.Combine(SoundboardFolder, "library.json");

    private void LoadSoundboard()
    {
        try
        {
            if (File.Exists(SoundboardSettingsPath))
                soundboardSettings = JsonSerializer.Deserialize<SoundboardSettings>(File.ReadAllText(SoundboardSettingsPath)) ?? new();
            soundboardSettings.Clips ??= [];
            soundboardSettings.Volume = Math.Clamp(soundboardSettings.Volume, 0, 100);
            soundboardSettings.Clips = soundboardSettings.Clips.Where(c => !string.IsNullOrWhiteSpace(c.Id) && c.FileName == Path.GetFileName(c.FileName)).Take(100).ToList();
            foreach (var clip in soundboardSettings.Clips) clip.Volume = Math.Clamp(clip.Volume, 0, 100);
            var usedPads = new HashSet<int>();
            foreach (var clip in soundboardSettings.Clips)
            {
                if (!PadColors.Contains(clip.Color)) clip.Color = PadColors[Random.Shared.Next(PadColors.Length)];
                if (clip.Pad < 0 || clip.Pad >= 108 || !usedPads.Add(clip.Pad))
                {
                    clip.Pad = Enumerable.Range(0, 108).First(p => !usedPads.Contains(p));
                    usedPads.Add(clip.Pad);
                }
            }
            soundboard.Gain = soundboardSettings.Volume / 100f;
            if (soundboardSettings.Clips.Count > 0) SaveSoundboard();
        }
        catch { soundboardSettings = new(); }
    }

    private void SaveSoundboard()
    {
        Directory.CreateDirectory(SoundboardFolder);
        var temp = SoundboardSettingsPath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(soundboardSettings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, SoundboardSettingsPath, true);
    }

    private static float[] DecodeClip(string path)
    {
        using var reader = new AudioFileReader(path);
        if (reader.TotalTime > TimeSpan.FromSeconds(120)) throw new InvalidOperationException("Use a sound clip of two minutes or less.");
        ISampleProvider samples = reader;
        if (samples.WaveFormat.Channels == 1) samples = new MonoToStereoSampleProvider(samples);
        if (samples.WaveFormat.Channels != 2) throw new InvalidOperationException("Use a mono or stereo audio file.");
        if (samples.WaveFormat.SampleRate != 48000) samples = new WdlResamplingSampleProvider(samples, 48000);
        var data = new List<float>();
        var buffer = new float[8192];
        int read;
        while ((read = samples.Read(buffer)) > 0)
        {
            if (data.Count + read > 48000 * 2 * 120) throw new InvalidOperationException("Use a sound clip of two minutes or less.");
            for (int i = 0; i < read; i++) data.Add(buffer[i]);
        }
        if (data.Count == 0) throw new InvalidOperationException("This file contains no playable audio.");
        return data.ToArray();
    }

    private async Task HandleSoundboardCommandAsync(JsonElement root, string command)
    {
        if (command == "soundPreviewStop") { StopSoundPreview(); return; }
        if (command == "soundPreview")
        {
            if (pendingSoundAudio is null) return;
            StopSoundPreview();
            var previewSamples = TrimSound(pendingSoundAudio, root.GetProperty("start").GetDouble(), root.GetProperty("end").GetDouble());
            soundPreviewStart = root.GetProperty("start").GetDouble();
            soundPreviewEnd = soundPreviewStart + previewSamples.Length / 96000d;
            soundPreviewStream = new RawSourceWaveStream(new MemoryStream(MemoryMarshal.AsBytes(previewSamples.AsSpan()).ToArray()), WaveFormat.CreateIeeeFloatWaveFormat(48000, 2));
            soundPreviewOutput = new WaveOut();
            soundPreviewOutput.Init(soundPreviewStream);
            soundPreviewOutput.Volume = Math.Clamp(root.GetProperty("value").GetInt32(), 0, 100) / 100f;
            soundPreviewOutput.Play();
            return;
        }
        if (command == "soundCancel") { StopSoundPreview(); pendingSoundFile = null; pendingSoundAudio = null; return; }
        if (command == "soundBrowse")
        {
            using var dialog = new OpenFileDialog { Title = "Choose a sound", Filter = "Audio clips|*.wav;*.mp3;*.aiff;*.aif;*.wma;*.m4a|All files|*.*", RestoreDirectory = true };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            if (new FileInfo(dialog.FileName).Length > 50 * 1024 * 1024) throw new InvalidOperationException("Use a file smaller than 50 MB.");
            StopSoundPreview();
            var decoded = await Task.Run(() => DecodeClip(dialog.FileName));
            pendingSoundFile = dialog.FileName;
            pendingSoundAudio = decoded;
            var peaks = new float[160];
            for (int i = 0; i < peaks.Length; i++)
                for (int j = i * decoded.Length / peaks.Length; j < (i + 1) * decoded.Length / peaks.Length; j++)
                    peaks[i] = Math.Max(peaks[i], Math.Min(1, Math.Abs(decoded[j])));
            webUi?.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { type = "soundFile", file = Path.GetFileName(pendingSoundFile), name = Path.GetFileNameWithoutExtension(pendingSoundFile), duration = decoded.Length / 96000d, peaks }));
            return;
        }
        if (command == "soundImport")
        {
            var source = pendingSoundFile;
            if (source is null || pendingSoundAudio is null) throw new InvalidOperationException("Choose an audio file first.");
            var name = root.GetProperty("name").GetString()?.Trim();
            if (string.IsNullOrEmpty(name) || name.Length > 60) throw new InvalidOperationException("Enter a sound name between 1 and 60 characters.");
            int volume = Math.Clamp(root.GetProperty("value").GetInt32(), 0, 100);
            var replaceId = root.TryGetProperty("id", out var idProperty) ? idProperty.GetString() : null;
            var replacement = soundboardSettings.Clips.FirstOrDefault(c => c.Id == replaceId);
            if (replacement is null && soundboardSettings.Clips.Count >= 100) throw new InvalidOperationException("Your soundboard can hold up to 100 clips.");
            int pad = replacement?.Pad ?? (root.TryGetProperty("pad", out var padProperty) ? padProperty.GetInt32() : Enumerable.Range(0, 108).First(p => soundboardSettings.Clips.All(c => c.Pad != p)));
            if (pad < 0 || pad >= 108 || soundboardSettings.Clips.Any(c => c.Pad == pad && c != replacement)) throw new InvalidOperationException("That pad is already assigned.");
            StopSoundPreview();
            var trimmed = TrimSoundForSave(pendingSoundAudio, root.GetProperty("start").GetDouble(), root.GetProperty("end").GetDouble());
            var clip = new SoundClip { Name = name, Pad = pad, Volume = volume, Color = PadColors[Random.Shared.Next(PadColors.Length)] };
            clip.FileName = clip.Id + ".wav";
            Directory.CreateDirectory(SoundboardFolder);
            using (var writer = new WaveFileWriter(Path.Combine(SoundboardFolder, clip.FileName), WaveFormat.CreateIeeeFloatWaveFormat(48000, 2)))
                writer.Write(MemoryMarshal.AsBytes(trimmed.AsSpan()));
            if (replacement is not null)
            {
                soundboard.Stop(replacement.Id);
                soundCache.Remove(replacement.Id);
                soundboardSettings.Clips.Remove(replacement);
            }
            soundboardSettings.Clips.Add(clip);
            SaveSoundboard();
            if (replacement is not null) File.Delete(Path.Combine(SoundboardFolder, replacement.FileName));
            pendingSoundFile = null;
            pendingSoundAudio = null;
            webUi?.CoreWebView2.PostWebMessageAsJson("{\"type\":\"soundSaved\"}");
            return;
        }
        if (command == "soundStop") { soundPlaybackEpoch++; soundboard.StopAll(); return; }
        if (command == "soundMaster")
        {
            soundboardSettings.Volume = Math.Clamp(root.GetProperty("value").GetInt32(), 0, 100);
            soundboard.Gain = soundboardSettings.Volume / 100f;
            SaveSoundboard(); return;
        }
        var id = root.GetProperty("id").GetString();
        var selected = soundboardSettings.Clips.FirstOrDefault(c => c.Id == id);
        if (selected is null) return;
        switch (command)
        {
            case "soundPlay":
                int playbackEpoch = soundPlaybackEpoch;
                await EnsureEngineRunningAsync();
                if (!running) throw new InvalidOperationException("Choose your microphone and CABLE Input destination before playing a clip.");
                if (!soundCache.TryGetValue(selected.Id, out var audio))
                {
                    audio = await Task.Run(() => DecodeClip(Path.Combine(SoundboardFolder, selected.FileName)));
                    // Bound cached decoded audio to roughly 92 MB; currently playing clips retain their data.
                    if (soundCache.Values.Sum(a => (long)a.Length) + audio.Length > 24_000_000) soundCache.Clear();
                    soundCache[selected.Id] = audio;
                }
                if (!closing && running && playbackEpoch == soundPlaybackEpoch) soundboard.Play(selected.Id, audio, selected.Volume / 100f);
                break;
            case "soundRename":
                var name = root.GetProperty("name").GetString()?.Trim();
                if (string.IsNullOrEmpty(name) || name.Length > 60) throw new InvalidOperationException("Enter a clip name between 1 and 60 characters.");
                selected.Name = name; SaveSoundboard(); break;
            case "soundVolume":
                selected.Volume = Math.Clamp(root.GetProperty("value").GetInt32(), 0, 100);
                soundboard.SetClipGain(selected.Id, selected.Volume / 100f);
                SaveSoundboard(); break;
            case "soundRemove":
                soundboard.Stop(selected.Id);
                File.Delete(Path.Combine(SoundboardFolder, selected.FileName));
                soundCache.Remove(selected.Id);
                soundboardSettings.Clips.Remove(selected); SaveSoundboard(); break;
        }
    }

    private sealed class SoundboardBus
    {
        private sealed class Voice(string id, float[] samples, float gain)
        {
            public string Id = id;
            public float[] Samples = samples;
            public int Position;
            public float Gain = gain;
        }
        private readonly object gate = new();
        private readonly List<Voice> voices = [];
        public float Gain = 1f;
        public string[] Playing { get { lock (gate) return voices.Select(v => v.Id).ToArray(); } }
        public void Play(string id, float[] samples, float gain)
        {
            lock (gate)
            {
                voices.RemoveAll(v => v.Id == id);
                if (voices.Count >= 8) voices.RemoveAt(0);
                voices.Add(new(id, samples, gain));
            }
        }
        public void Stop(string id) { lock (gate) voices.RemoveAll(v => v.Id == id); }
        public void StopAll() { lock (gate) voices.Clear(); }
        public void SetClipGain(string id, float gain) { lock (gate) foreach (var v in voices) if (v.Id == id) v.Gain = gain; }
        public void AddTo(Span<float> output, float master)
        {
            lock (gate)
            {
                foreach (var voice in voices)
                {
                    int count = Math.Min(output.Length, voice.Samples.Length - voice.Position);
                    float gain = voice.Gain * Gain * master;
                    for (int i = 0; i < count; i++) output[i] += voice.Samples[voice.Position + i] * gain;
                    voice.Position += count;
                }
                voices.RemoveAll(v => v.Position >= v.Samples.Length);
            }
        }
    }
}
