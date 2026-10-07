using System;
using System.Windows.Forms;

namespace ProgramMic;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        var identity = System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value;
        var prefix = @"Local\ProgramMic." + identity;
        using var instance = new System.Threading.Mutex(true, prefix + ".Instance", out bool firstInstance);
        using var showSignal = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, prefix + ".Show");
        using var exitSignal = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, prefix + ".Exit");
        if (!firstInstance)
        {
            if (args.Contains("--exit")) exitSignal.Set();
            else showSignal.Set();
            return;
        }
        if (args.Contains("--exit")) return;
        ApplicationConfiguration.Initialize();
        using var form = new MainForm();
        using var signals = new System.Windows.Forms.Timer { Interval = 250 };
        signals.Tick += (_, _) =>
        {
            if (exitSignal.WaitOne(0)) { form.ExitFromTray(); return; }
            if (showSignal.WaitOne(0)) form.RestoreFromTray();
        };
        signals.Start();
        Application.Run(form);
    }
}
