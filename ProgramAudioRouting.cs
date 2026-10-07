namespace ProgramMic;

public sealed partial class MainForm
{
    private int engineEpoch;
    private bool changingProgramAudio;

    private void StopProgramCapture()
    {
        engineEpoch++;
        programEnabled = false;
        var recorder = programRecorder;
        programRecorder = null;
        if (recorder is not null)
        {
            recorder.DataAvailable -= ProgramData;
            recorder.RecordingStopped -= Stopped;
            try { recorder.StopRecording(); recorder.Dispose(); } catch { }
        }
        programBuffer?.ClearBuffer();
        UpdateGains(); UpdateToggleUi();
    }

    private async Task ChangeProgramSourceAsync()
    {
        if (restartingEngine || closing) return;
        bool resume = programEnabled;
        StopProgramCapture();
        if (resume) await ToggleProgramAudioAsync();
    }

    private async Task ToggleProgramAudioAsync()
    {
        if (!running || closing || changingProgramAudio) return;
        changingProgramAudio = true;
        try
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
                throw new NotSupportedException("Application audio capture requires Windows 10 build 19041 or newer.");
            if (programEnabled)
            {
                programEnabled = false;
                programBuffer?.ClearBuffer();
                UpdateGains(); UpdateToggleUi();
                return;
            }
            if (processBox.SelectedItem is not ProcessItem process)
            {
                MessageBox.Show(this, "Choose an application to add its audio. Your microphone is still routed to VB-CABLE.", "ProgramMic");
                return;
            }
            if (programRecorder is null)
            {
                int epoch = engineEpoch;
                var recorder = await new NAudio.Wave.WasapiRecorderBuilder()
                    .WithProcessLoopback((uint)process.Pid, NAudio.CoreAudioApi.ProcessLoopbackMode.IncludeTargetProcessTree)
                    .WithEventSync().WithBufferLength(50).WithFormat(AudioFormat).BuildAsync();
                if (epoch != engineEpoch || closing || !running)
                {
                    recorder.Dispose();
                    return;
                }
                programRecorder = recorder;
                recorder.DataAvailable += ProgramData;
                recorder.RecordingStopped += Stopped;
                recorder.StartRecording();
            }
            programBuffer?.ClearBuffer();
            programEnabled = true;
            UpdateGains(); UpdateToggleUi();
        }
        catch (Exception ex)
        {
            programEnabled = false;
            if (programRecorder is not null)
            {
                programRecorder.DataAvailable -= ProgramData;
                programRecorder.RecordingStopped -= Stopped;
                try { programRecorder.Dispose(); } catch { }
                programRecorder = null;
            }
            UpdateGains(); UpdateToggleUi();
            if (!closing) MessageBox.Show(this, "Could not add application audio. Your microphone remains routed to VB-CABLE.\n\n" + ex.Message, "ProgramMic", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { changingProgramAudio = false; PublishWebState(); }
    }
}
