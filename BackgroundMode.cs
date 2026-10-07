namespace ProgramMic;

public sealed partial class MainForm
{
    private NotifyIcon? trayIcon;
    private ContextMenuStrip? trayMenu;
    private bool exitRequested;
    private bool trayHintShown;

    private void InitializeBackgroundMode()
    {
        try
        {
            var executable = Path.Combine(AppContext.BaseDirectory, "ProgramMic.exe");
            if (File.Exists(executable))
            {
                using var startup = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                startup.SetValue("ProgramMic", "\"" + executable + "\" --tray");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not enable start with Windows. ProgramMic can still run in the system tray.\n\n" + ex.Message,
                "ProgramMic Startup", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Open ProgramMic", null, (_, _) => RestoreFromTray());
        trayMenu.Items.Add("Toggle ProgramMic", null, async (_, _) =>
        {
            await EnsureEngineRunningAsync();
            if (running) ToggleProgram();
        });
        trayMenu.Items.Add("Toggle Microphone", null, (_, _) => ToggleMicrophone());
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add("Exit ProgramMic", null, (_, _) => ExitFromTray());
        trayIcon = new NotifyIcon
        {
            Icon = Icon ?? SystemIcons.Application,
            Text = "ProgramMic — running in background",
            ContextMenuStrip = trayMenu,
            Visible = true
        };
        trayIcon.DoubleClick += (_, _) => RestoreFromTray();
        Resize += (_, _) => { if (WindowState == FormWindowState.Minimized) HideToTray(); };
        Shown += (_, _) =>
        {
            if (Environment.GetCommandLineArgs().Contains("--tray")) BeginInvoke(HideToTray);
        };
        FormClosed += (_, _) => { trayIcon.Visible = false; trayIcon.Dispose(); trayMenu.Dispose(); };
    }

    private void HideToTray()
    {
        SaveSettings();
        CancelHotkeyAssignment();
        Hide();
        if (trayHintShown || trayIcon is null) return;
        trayHintShown = true;
        trayIcon.ShowBalloonTip(3000, "ProgramMic is still running",
            "Audio routing and hotkeys stay active. Use the tray icon to reopen ProgramMic or exit.", ToolTipIcon.Info);
    }

    internal void RestoreFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    internal void ExitFromTray()
    {
        exitRequested = true;
        Close();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!exitRequested && e.CloseReason is CloseReason.UserClosing or CloseReason.TaskManagerClosing)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }
        base.OnFormClosing(e);
    }
}
