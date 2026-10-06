using System.Diagnostics;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Reflection;
using System.Text.Json;

namespace ProgramMic;

public sealed class MainForm : Form
{
    static readonly Color Bg=Color.FromArgb(18,18,22), Panel=Color.FromArgb(27,27,33), Field=Color.FromArgb(37,37,45), Border=Color.FromArgb(62,62,72), TextPrimary=Color.FromArgb(245,245,247), TextSecondary=Color.FromArgb(160,160,172), Pink=Color.FromArgb(255,79,154), PinkBright=Color.FromArgb(255,107,171);
    private readonly ComboBox processBox=new(), micBox=new(), outputBox=new();
    private readonly PinkSlider programVolume=new(), micVolume=new(), masterVolume=new();
    private readonly Label programVolumeLabel=new(), micVolumeLabel=new(), masterVolumeLabel=new(), statusLabel=new(), hotkeyLabel=new();
    private readonly Button refreshButton=new(), toggleButton=new(), setHotkeyButton=new();
    private readonly System.Windows.Forms.Timer refreshTimer=new();

    private WasapiRecorder? micRecorder, programRecorder;
    private WasapiPlayer? player;
    private BufferedWaveProvider? micBuffer, programBuffer;
    private TwoInputFloatMixer? mixer;
    private bool running, programEnabled, closing, assigningHotkey;

    private static readonly WaveFormat AudioFormat=WaveFormat.CreateIeeeFloatWaveFormat(48000,2);

    private const int HOTKEY_ID=0x504D, WM_HOTKEY=0x0312;
    private Keys hotkeyKey=Keys.F8;
    private Mods hotkeyMods=Mods.None;

    private const string AppVersion="v1.0.0";

    private static readonly string SettingsDirectory=
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"ProgramMic");
    private static readonly string SettingsPath=Path.Combine(SettingsDirectory,"settings.json");
    private bool loadingSettings;
    [Flags] private enum Mods:uint { None=0, Alt=1, Control=2, Shift=4, Win=8, NoRepeat=0x4000 }
    [DllImport("user32.dll",SetLastError=true)] private static extern bool RegisterHotKey(IntPtr hWnd,int id,uint mods,uint key);
    [DllImport("user32.dll",SetLastError=true)] private static extern bool UnregisterHotKey(IntPtr hWnd,int id);

    public MainForm()
    {
        Text="ProgramMic";
        ClientSize=new Size(640,760);
        MinimumSize=new Size(656,799);
        BackColor=Bg;
        ForeColor=TextPrimary;
        Font=new Font("Segoe UI",9f);
        StartPosition=FormStartPosition.CenterScreen;

        // ProgramMic application/taskbar icon, embedded in the executable.
        var iconStream=Assembly.GetExecutingAssembly().GetManifestResourceStream("ProgramMic.ProgramMic.ico");
        if(iconStream!=null)
            Icon=new Icon(iconStream);

        // Root layout: the UI is built from rows instead of hard-coded Y coordinates.
        var root=new TableLayoutPanel
        {
            Dock=DockStyle.Fill,
            BackColor=Bg,
            Padding=new Padding(28,22,28,22),
            ColumnCount=1,
            RowCount=9,
            AutoScroll=true
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));

        var title=new Label
        {
            Text="ProgramMic",
            Font=new Font("Segoe UI",20,FontStyle.Bold),
            ForeColor=TextPrimary,
            BackColor=Bg,
            AutoSize=true,
            Margin=new Padding(0,0,0,0)
        };
        var sub=new Label
        {
            Text="APPLICATION AUDIO ROUTER",
            Font=new Font("Segoe UI",8,FontStyle.Bold),
            ForeColor=Pink,
            BackColor=Bg,
            AutoSize=true,
            Margin=new Padding(0,0,0,0)
        };
        var brandText=new FlowLayoutPanel
        {
            FlowDirection=FlowDirection.TopDown,
            WrapContents=false,
            AutoSize=true,
            AutoSizeMode=AutoSizeMode.GrowAndShrink,
            Margin=new Padding(0,0,0,0)
        };
        brandText.Controls.Add(title);
        brandText.Controls.Add(sub);

        var brandIcon=new PictureBox
        {
            Size=new Size(42,42),
            SizeMode=PictureBoxSizeMode.Zoom,
            BackColor=Bg,
            Margin=new Padding(0,2,10,0)
        };
        if(Icon!=null)
            brandIcon.Image=Icon.ToBitmap();

        var brand=new FlowLayoutPanel
        {
            FlowDirection=FlowDirection.LeftToRight,
            WrapContents=false,
            AutoSize=true,
            AutoSizeMode=AutoSizeMode.GrowAndShrink,
            Dock=DockStyle.Top,
            Margin=new Padding(0,0,0,20)
        };
        brand.Controls.Add(brandIcon);
        brand.Controls.Add(brandText);

        var programSection=CreateAudioSection("PROGRAM AUDIO",processBox,programVolumeLabel,programVolume,true);
        programSection.Margin=new Padding(0,0,0,32);
        var micSection=CreateAudioSection("YOUR MICROPHONE",micBox,micVolumeLabel,micVolume,false);
        var outputSection=CreateAudioSection("VIRTUAL MIC DESTINATION",outputBox,masterVolumeLabel,masterVolume,false);

        processBox.DropDownStyle=ComboBoxStyle.DropDownList; StyleCombo(processBox);
        micBox.DropDownStyle=ComboBoxStyle.DropDownList; StyleCombo(micBox);
        outputBox.DropDownStyle=ComboBoxStyle.DropDownList; StyleCombo(outputBox);

        programVolumeLabel.Text="Program volume: 100%";
        micVolumeLabel.Text="Mic volume: 100%";
        masterVolumeLabel.Text="VB-CABLE output: 100%";

        ConfigureTrackBar(programVolume,0,100,100);
        ConfigureTrackBar(micVolume,0,150,100);
        ConfigureTrackBar(masterVolume,0,150,100);

        refreshButton.Text="";
        refreshButton.Size=new Size(48,28);
        refreshButton.Margin=new Padding(8,0,0,0);
        refreshButton.Padding=new Padding(0);
        refreshButton.UseVisualStyleBackColor=false;
        StyleButton(refreshButton,false);
        refreshButton.BackColor=Field;
        refreshButton.ForeColor=Color.White;
        refreshButton.FlatAppearance.BorderColor=Border;
        refreshButton.Paint+=(s,e)=>
        {
            e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

            // Approved fixed 24x24 reload vector, rendered at 18x18 and centered.
            using var path=new System.Drawing.Drawing2D.GraphicsPath(System.Drawing.Drawing2D.FillMode.Winding);

            path.AddBezier(
                new PointF(4.15f,9.15f), new PointF(5.45f,5.35f),
                new PointF(9.15f,3.00f), new PointF(13.15f,3.00f));
            path.AddBezier(
                new PointF(13.15f,3.00f), new PointF(16.25f,3.00f),
                new PointF(18.85f,4.45f), new PointF(20.45f,6.75f));
            path.AddLine(new PointF(20.45f,6.75f),new PointF(22.35f,4.85f));
            path.AddLine(new PointF(22.35f,4.85f),new PointF(22.35f,11.10f));
            path.AddLine(new PointF(22.35f,11.10f),new PointF(16.10f,11.10f));
            path.AddLine(new PointF(16.10f,11.10f),new PointF(18.00f,9.20f));
            path.AddBezier(
                new PointF(18.00f,9.20f), new PointF(16.95f,7.35f),
                new PointF(15.10f,6.15f), new PointF(13.05f,6.15f));
            path.AddBezier(
                new PointF(13.05f,6.15f), new PointF(10.45f,6.15f),
                new PointF(8.15f,7.65f), new PointF(7.10f,9.95f));
            path.CloseFigure();

            path.AddBezier(
                new PointF(19.85f,14.85f), new PointF(18.55f,18.65f),
                new PointF(14.85f,21.00f), new PointF(10.85f,21.00f));
            path.AddBezier(
                new PointF(10.85f,21.00f), new PointF(7.75f,21.00f),
                new PointF(5.15f,19.55f), new PointF(3.55f,17.25f));
            path.AddLine(new PointF(3.55f,17.25f),new PointF(1.65f,19.15f));
            path.AddLine(new PointF(1.65f,19.15f),new PointF(1.65f,12.90f));
            path.AddLine(new PointF(1.65f,12.90f),new PointF(7.90f,12.90f));
            path.AddLine(new PointF(7.90f,12.90f),new PointF(6.00f,14.80f));
            path.AddBezier(
                new PointF(6.00f,14.80f), new PointF(7.05f,16.65f),
                new PointF(8.90f,17.85f), new PointF(10.95f,17.85f));
            path.AddBezier(
                new PointF(10.95f,17.85f), new PointF(13.55f,17.85f),
                new PointF(15.85f,16.35f), new PointF(16.90f,14.05f));
            path.CloseFigure();

            var state=e.Graphics.Save();
            e.Graphics.TranslateTransform((refreshButton.ClientSize.Width-18f)/2f,(refreshButton.ClientSize.Height-18f)/2f);
            e.Graphics.ScaleTransform(18f/24f,18f/24f);
            using var brush=new SolidBrush(Color.White);
            e.Graphics.FillPath(brush,path);
            e.Graphics.Restore(state);
        };
        var hotkeyHeader=new Label
        {
            Text="HOTKEY",Font=new Font("Segoe UI",9,FontStyle.Bold),
            ForeColor=TextPrimary,BackColor=Bg,AutoSize=true,
            Margin=new Padding(0,0,0,8)
        };
        hotkeyLabel.Text="F8";
        hotkeyLabel.ForeColor=TextSecondary;
        hotkeyLabel.AutoSize=true;
        hotkeyLabel.Margin=new Padding(0,9,34,0);
        setHotkeyButton.Text="ASSIGN HOTKEY";
        setHotkeyButton.Size=new Size(145,34);
        StyleButton(setHotkeyButton,false);

        var hotkeyRow=new FlowLayoutPanel
        {
            FlowDirection=FlowDirection.LeftToRight,WrapContents=false,
            AutoSize=true,Dock=DockStyle.Top,Margin=new Padding(0)
        };
        hotkeyRow.Controls.Add(hotkeyLabel);
        hotkeyRow.Controls.Add(setHotkeyButton);
        var hotkeySection=new FlowLayoutPanel
        {
            FlowDirection=FlowDirection.TopDown,WrapContents=false,
            AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,
            Dock=DockStyle.Top,Margin=new Padding(0,0,0,22)
        };
        hotkeySection.Controls.Add(hotkeyHeader);
        hotkeySection.Controls.Add(hotkeyRow);

        toggleButton.Text="PROGRAM AUDIO: OFF   (F8)";
        toggleButton.Font=new Font("Segoe UI",11,FontStyle.Bold);
        toggleButton.Height=48;
        toggleButton.Dock=DockStyle.Top;
        toggleButton.Margin=new Padding(0,0,0,12);
        StyleButton(toggleButton,true);
        toggleButton.Enabled=true;

        statusLabel.Text="● MIC LIVE — program audio muted";
        statusLabel.ForeColor=PinkBright;
        statusLabel.BackColor=Bg;
        statusLabel.AutoSize=true;
        statusLabel.Margin=new Padding(0,0,0,18);

        var infoIcon=new Label
        {
            Text="ⓘ",Font=new Font("Segoe UI Symbol",13,FontStyle.Regular),
            ForeColor=Pink,BackColor=Bg,AutoSize=true,
            Margin=new Padding(0,3,8,0),Cursor=Cursors.Hand
        };
        var infoText=new Label
        {
            Text="Discord / voice chat audio tips",
            Font=new Font("Segoe UI",8,FontStyle.Regular),
            ForeColor=TextSecondary,BackColor=Bg,AutoSize=true,
            Margin=new Padding(0,6,0,0),Cursor=Cursors.Hand
        };
        var infoRow=new FlowLayoutPanel
        {
            FlowDirection=FlowDirection.LeftToRight,WrapContents=false,
            AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,
            Dock=DockStyle.Top,Margin=new Padding(0,4,0,12),Cursor=Cursors.Hand
        };
        infoRow.Controls.Add(infoIcon);
        infoRow.Controls.Add(infoText);

        void ShowDiscordTip()
        {
            MessageBox.Show(
                this,
                "For clearer program audio in Discord, disable:\\n\\n" +
                "• Noise Suppression\\n" +
                "• Echo Cancellation\\n" +
                "• Automatic Gain Control\\n\\n" +
                "These voice-processing features can make routed application audio sound muffled, distorted, or choppy.",
                "Discord / Voice Chat Tip",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        infoIcon.Click+=(_,_)=>ShowDiscordTip();
        infoText.Click+=(_,_)=>ShowDiscordTip();
        infoRow.Click+=(_,_)=>ShowDiscordTip();

        root.Controls.Add(brand);
        root.Controls.Add(programSection);
        root.Controls.Add(micSection);
        root.Controls.Add(outputSection);
        root.Controls.Add(hotkeySection);
        root.Controls.Add(toggleButton);
        root.Controls.Add(statusLabel);
        root.Controls.Add(infoRow);
                var footer=new Panel
        {
            Height=30,
            Dock=DockStyle.Bottom,
            BackColor=Bg,
            Padding=new Padding(6,0,6,8),
            Margin=new Padding(0)
        };

        var versionLabel=new Label
        {
            Text=AppVersion,
            AutoSize=true,
            ForeColor=TextSecondary,
            Font=new Font("Segoe UI",7.5f,FontStyle.Regular),
            Location=new Point(6,5)
        };

        var creatorLink=new LinkLabel
        {
            Text="@tpavz",
            AutoSize=true,
            LinkColor=PinkBright,
            ActiveLinkColor=Color.White,
            VisitedLinkColor=PinkBright,
            Font=new Font("Segoe UI",7.5f,FontStyle.Regular),
            LinkBehavior=LinkBehavior.HoverUnderline,
            Anchor=AnchorStyles.Top|AnchorStyles.Right
        };
        creatorLink.LinkClicked+=(_,_)=>
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName="https://discord.com/users/355793811827458049",
                    UseShellExecute=true
                });
            }
            catch { }
        };

        footer.Controls.Add(versionLabel);
        footer.Controls.Add(creatorLink);

        void PositionFooterItems()
        {
            creatorLink.Location=new Point(
                footer.ClientSize.Width-creatorLink.Width-6,
                5);
        }

        footer.Resize+=(_,_)=>PositionFooterItems();
        PositionFooterItems();

Controls.Add(root);
        Controls.Add(footer);

        // Existing behavior
        refreshButton.Click+=async(_,_)=>{StopEngine();LoadDevices();LoadProcesses();await EnsureEngineRunningAsync();};
        setHotkeyButton.Click+=(_,_)=>BeginAssignHotkey();
        toggleButton.Click+=async(_,_)=>{if(!running) await EnsureEngineRunningAsync(); if(running) ToggleProgram();};

        programVolume.ValueChanged+=(_,_)=>{programVolumeLabel.Text=$"Program volume: {programVolume.Value}%";UpdateGains();SaveSettings();};
        micVolume.ValueChanged+=(_,_)=>{micVolumeLabel.Text=$"Mic volume: {micVolume.Value}%";UpdateGains();SaveSettings();};
        masterVolume.ValueChanged+=(_,_)=>{masterVolumeLabel.Text=$"VB-CABLE output: {masterVolume.Value}%";UpdateGains();SaveSettings();};

        LoadDevices();
        LoadProcesses();
        LoadSettings();

        processBox.SelectedIndexChanged+=async(_,_)=>{SaveSettings();await RestartEngineForSelectionChangeAsync();};
        micBox.SelectedIndexChanged+=async(_,_)=>{SaveSettings();await RestartEngineForSelectionChangeAsync();};
        outputBox.SelectedIndexChanged+=async(_,_)=>{SaveSettings();await RestartEngineForSelectionChangeAsync();};

        refreshTimer.Interval=2000;
        refreshTimer.Tick+=(_,_)=>{ };
        refreshTimer.Start();

        Shown+=async(_,_)=>{RegisterCurrentHotkey(false);await EnsureEngineRunningAsync();};
        FormClosing+=(_,_)=>{SaveSettings();closing=true;refreshTimer.Stop();StopEngine();UnregisterHotKey(Handle,HOTKEY_ID);};
    }

    private Control CreateAudioSection(string heading, ComboBox combo, Label volumeLabel, PinkSlider slider, bool withRefresh)
    {
        var section=new TableLayoutPanel
        {
            ColumnCount=1,RowCount=4,Dock=DockStyle.Top,AutoSize=true,
            AutoSizeMode=AutoSizeMode.GrowAndShrink,
            BackColor=Bg,Margin=new Padding(0,0,0,22),Padding=new Padding(0)
        };
        section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));

        Control header;
        if(withRefresh)
        {
            var row=new TableLayoutPanel
            {
                ColumnCount=3,RowCount=1,Dock=DockStyle.Top,Height=32,
                Margin=new Padding(0,0,0,8),Padding=new Padding(0)
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var h=new Label
            {
                Text=heading,Font=new Font("Segoe UI",9,FontStyle.Bold),
                ForeColor=TextPrimary,BackColor=Bg,AutoSize=true,
                Anchor=AnchorStyles.Left,Margin=new Padding(0,7,0,0)
            };
            var r=new Label
            {
                Text="REFRESH APPS",Font=new Font("Segoe UI",7,FontStyle.Bold),
                ForeColor=TextSecondary,BackColor=Bg,AutoSize=true,
                Anchor=AnchorStyles.Right,Margin=new Padding(0,9,8,0)
            };

            refreshButton.Anchor=AnchorStyles.Right;
            refreshButton.Margin=new Padding(0,2,0,2);

            row.Controls.Add(h,0,0);
            row.Controls.Add(r,1,0);
            row.Controls.Add(refreshButton,2,0);
            header=row;
        }
        else
        {
            header=new Label{Text=heading,Font=new Font("Segoe UI",9,FontStyle.Bold),ForeColor=TextPrimary,BackColor=Bg,AutoSize=true,Margin=new Padding(0,0,0,8)};
        }

        combo.Dock=DockStyle.Top;
        combo.Height=30;
        combo.Margin=new Padding(0,0,0,8);

        volumeLabel.ForeColor=TextPrimary;
        volumeLabel.BackColor=Bg;
        volumeLabel.AutoSize=true;
        volumeLabel.Margin=new Padding(0,0,0,5);

        slider.Dock=DockStyle.Top;
        slider.Margin=new Padding(0);
        slider.Height=34;

        section.Controls.Add(header,0,0);
        section.Controls.Add(combo,0,1);
        section.Controls.Add(volumeLabel,0,2);
        section.Controls.Add(slider,0,3);
        return section;
    }

    private static void ConfigureTrackBar(PinkSlider bar,int min,int max,int value)
    {
        bar.Minimum=min; bar.Maximum=max; bar.Value=value; bar.TickFrequency=10;
        bar.Height=34;
    }

    private static Label Header(string text,int y)=>new(){Text=text,Font=new Font("Segoe UI",9,FontStyle.Bold),AutoSize=true,Location=new Point(28,y)};
    private static void StyleCombo(ComboBox box)
    {
        box.BackColor = Field;
        box.ForeColor = TextPrimary;
        box.FlatStyle = FlatStyle.Flat;
    }

    private static void StyleButton(Button button, bool accent)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = accent ? Pink : Border;
        button.BackColor = accent ? Panel : Field;
        button.ForeColor = Color.White;
        button.Cursor = Cursors.Hand;
    }
    private void BuildUi()
    {
        var title=new Label{Text="ProgramMic",Font=new Font("Segoe UI",20,FontStyle.Bold),ForeColor=TextPrimary,BackColor=Bg,AutoSize=true,Location=new Point(24,18)};
        var sub=new Label{Text="APPLICATION AUDIO ROUTER",Font=new Font("Segoe UI",8,FontStyle.Bold),AutoSize=true,ForeColor=Pink,BackColor=Bg,Location=new Point(27,56)};
        processBox.DropDownStyle=micBox.DropDownStyle=outputBox.DropDownStyle=ComboBoxStyle.DropDownList;
        processBox.SetBounds(28,116,570,30); micBox.SetBounds(28,250,570,30); outputBox.SetBounds(28,384,570,30);
        foreach(var b in new[]{processBox,micBox,outputBox}) b.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;

        programVolumeLabel.Text="Program volume: 100%"; programVolumeLabel.SetBounds(28,154,180,20);
        programVolume.SetBounds(160,141,438,45); programVolume.Minimum=0; programVolume.Maximum=100; programVolume.Value=100; programVolume.TickFrequency=10; programVolume.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
        programVolume.ValueChanged+=(_,_)=>{programVolumeLabel.Text=$"Program volume: {programVolume.Value}%"; UpdateGains();};
        micVolumeLabel.Text="Mic volume: 100%"; micVolumeLabel.SetBounds(28,254,110,22);
        micVolume.SetBounds(135,244,463,45); micVolume.Minimum=0; micVolume.Maximum=150; micVolume.Value=100; micVolume.TickFrequency=10; micVolume.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
        micVolume.ValueChanged+=(_,_)=>{micVolumeLabel.Text=$"Mic volume: {micVolume.Value}%"; UpdateGains();};

        masterVolumeLabel.Text="VB-CABLE output: 100%"; masterVolumeLabel.SetBounds(28,422,180,20);
        masterVolume.SetBounds(28,444,570,24); masterVolume.Minimum=0; masterVolume.Maximum=150; masterVolume.Value=100; masterVolume.TickFrequency=10; masterVolume.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
        masterVolume.ValueChanged+=(_,_)=>{masterVolumeLabel.Text=$"VB-CABLE output: {masterVolume.Value}%"; UpdateGains();};

        var hotkeyHeader=Header("HOTKEY",494);
        hotkeyLabel.Text="F8"; hotkeyLabel.ForeColor=TextSecondary; hotkeyLabel.SetBounds(28,520,120,28);
        setHotkeyButton.Text="ASSIGN HOTKEY"; setHotkeyButton.SetBounds(155,512,145,34); StyleButton(setHotkeyButton,false); setHotkeyButton.Click+=(_,_)=>BeginAssignHotkey();
        var refreshLabel=new Label{Text="REFRESH APPS",Font=new Font("Segoe UI",7,FontStyle.Bold),ForeColor=TextSecondary,BackColor=Bg,AutoSize=true,Location=new Point(468,92)};
        refreshButton.Text="↻"; refreshButton.Font=new Font("Segoe UI Symbol",13,FontStyle.Regular); refreshButton.SetBounds(548,86,50,26); StyleButton(refreshButton,false); refreshButton.Click+=async(_,_)=>{StopEngine();LoadDevices();LoadProcesses();await EnsureEngineRunningAsync();};
        toggleButton.Text="PROGRAM AUDIO: OFF"; toggleButton.Font=new Font("Segoe UI",11,FontStyle.Bold); toggleButton.SetBounds(28,570,570,48); StyleButton(toggleButton,true); toggleButton.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right; toggleButton.Enabled=true; toggleButton.Click+=async(_,_)=>{if(!running) await EnsureEngineRunningAsync(); if(running) ToggleProgram();};
        statusLabel.Text="● READY — microphone routing starts automatically"; statusLabel.ForeColor=TextSecondary; statusLabel.AutoSize=true; statusLabel.Location=new Point(28,631);
        var tipPanel=new Panel{BackColor=Panel,BorderStyle=BorderStyle.FixedSingle,Location=new Point(28,662),Size=new Size(570,88),Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right};
        var discordTip=new Label{Text="DISCORD / VOICE CHAT TIP",Font=new Font("Segoe UI",8,FontStyle.Bold),ForeColor=Pink,BackColor=Panel,AutoSize=true,Location=new Point(11,10)};
        var discordText=new Label{Text="For clearer program audio, disable Noise Suppression, Echo Cancellation, and Automatic Gain Control in Discord Voice & Video settings.",ForeColor=TextSecondary,BackColor=Panel,AutoSize=false,Size=new Size(535,42),Location=new Point(11,31)};
        tipPanel.Controls.Add(discordTip); tipPanel.Controls.Add(discordText);

        Controls.AddRange([title,sub,Header("PROGRAM AUDIO",92),processBox,programVolumeLabel,programVolume,Header("YOUR MICROPHONE",226),micBox,micVolumeLabel,micVolume,Header("VIRTUAL MIC DESTINATION",360),outputBox,masterVolumeLabel,masterVolume,hotkeyHeader,hotkeyLabel,setHotkeyButton,refreshLabel,refreshButton,toggleButton,statusLabel,tipPanel]);
    }

    private void LoadSettings()
    {
        loadingSettings=true;
        try
        {
            if(!File.Exists(SettingsPath)) return;

            var json=File.ReadAllText(SettingsPath);
            var settings=JsonSerializer.Deserialize<AppSettings>(json);
            if(settings is null) return;

            SelectDeviceById(micBox,settings.MicrophoneDeviceId);
            SelectDeviceById(outputBox,settings.OutputDeviceId);
            SelectProcessByName(settings.ProcessName);

            programVolume.Value=Math.Clamp(settings.ProgramVolume,programVolume.Minimum,programVolume.Maximum);
            micVolume.Value=Math.Clamp(settings.MicVolume,micVolume.Minimum,micVolume.Maximum);
            masterVolume.Value=Math.Clamp(settings.OutputVolume,masterVolume.Minimum,masterVolume.Maximum);

            if(Enum.IsDefined(typeof(Keys),settings.HotkeyKey))
                hotkeyKey=(Keys)settings.HotkeyKey;

            hotkeyMods=(Mods)(settings.HotkeyModifiers & 0x000F);
            hotkeyLabel.Text=HotkeyText();
            UpdateToggleUi();
        }
        catch
        {
            // A missing/corrupt settings file should never prevent ProgramMic from starting.
        }
        finally
        {
            loadingSettings=false;
        }
    }

    private void SaveSettings()
    {
        if(loadingSettings || closing) return;

        try
        {
            Directory.CreateDirectory(SettingsDirectory);

            var settings=new AppSettings
            {
                ProcessName=(processBox.SelectedItem as ProcessItem)?.ProcessName,
                MicrophoneDeviceId=(micBox.SelectedItem as DeviceItem)?.Id,
                OutputDeviceId=(outputBox.SelectedItem as DeviceItem)?.Id,
                ProgramVolume=programVolume.Value,
                MicVolume=micVolume.Value,
                OutputVolume=masterVolume.Value,
                HotkeyKey=(int)hotkeyKey,
                HotkeyModifiers=(uint)(hotkeyMods & ~Mods.NoRepeat)
            };

            File.WriteAllText(
                SettingsPath,
                JsonSerializer.Serialize(settings,new JsonSerializerOptions{WriteIndented=true}));
        }
        catch
        {
            // Settings persistence is non-critical; audio routing should keep working.
        }
    }

    private static void SelectDeviceById(ComboBox box,string? id)
    {
        if(string.IsNullOrWhiteSpace(id)) return;
        for(int i=0;i<box.Items.Count;i++)
        {
            if(box.Items[i] is DeviceItem item &&
               string.Equals(item.Id,id,StringComparison.Ordinal))
            {
                box.SelectedIndex=i;
                return;
            }
        }
    }

    private void SelectProcessByName(string? processName)
    {
        if(string.IsNullOrWhiteSpace(processName)) return;
        for(int i=0;i<processBox.Items.Count;i++)
        {
            if(processBox.Items[i] is ProcessItem item &&
               string.Equals(item.ProcessName,processName,StringComparison.OrdinalIgnoreCase))
            {
                processBox.SelectedIndex=i;
                return;
            }
        }
    }

    private void LoadDevices()
    {
        if(running)return; string? oldMic=(micBox.SelectedItem as DeviceItem)?.Id, oldOut=(outputBox.SelectedItem as DeviceItem)?.Id;
        micBox.Items.Clear(); outputBox.Items.Clear(); using var en=new MMDeviceEnumerator();
        foreach(var d in en.EnumerateAudioEndPoints(DataFlow.Capture,DeviceState.Active)) if(!d.FriendlyName.Contains("CABLE Output",StringComparison.OrdinalIgnoreCase)) micBox.Items.Add(new DeviceItem(d));
        foreach(var d in en.EnumerateAudioEndPoints(DataFlow.Render,DeviceState.Active)) if(d.FriendlyName.Contains("CABLE Input",StringComparison.OrdinalIgnoreCase)||d.FriendlyName.Contains("VB-Audio",StringComparison.OrdinalIgnoreCase)) outputBox.Items.Add(new DeviceItem(d));
        Restore(micBox,oldMic); Restore(outputBox,oldOut); if(micBox.SelectedIndex<0&&micBox.Items.Count>0)micBox.SelectedIndex=0; if(outputBox.SelectedIndex<0&&outputBox.Items.Count>0)outputBox.SelectedIndex=0;
    }
    private static void Restore(ComboBox box,string? id){if(id==null)return;for(int i=0;i<box.Items.Count;i++)if(box.Items[i] is DeviceItem d&&d.Id==id){box.SelectedIndex=i;break;}}

    private void LoadProcesses()
    {
        if(running)return; int? old=(processBox.SelectedItem as ProcessItem)?.Pid; var list=new List<ProcessItem>();
        foreach(var p in Process.GetProcesses()) try{if(!p.HasExited&&p.Id!=Environment.ProcessId&&(p.MainWindowHandle!=IntPtr.Zero||!string.IsNullOrWhiteSpace(p.MainWindowTitle)))list.Add(new ProcessItem(p.Id,p.ProcessName,p.MainWindowTitle));}catch{}finally{p.Dispose();}
        list=list.GroupBy(x=>x.Pid).Select(g=>g.First()).OrderBy(x=>x.DisplayName).ToList(); processBox.Items.Clear(); foreach(var x in list)processBox.Items.Add(x);
        if(old.HasValue){int i=list.FindIndex(x=>x.Pid==old);if(i>=0)processBox.SelectedIndex=i;} if(processBox.SelectedIndex<0&&processBox.Items.Count>0)processBox.SelectedIndex=0;
    }

    private bool restartingEngine;
    private async Task EnsureEngineRunningAsync()
    {
        if (running || restartingEngine || closing) return;
        if (processBox.SelectedItem is null || micBox.SelectedItem is null || outputBox.SelectedItem is null) return;
        await StartEngineAsync();
    }

    private async Task RestartEngineForSelectionChangeAsync()
    {
        if (restartingEngine || closing || !IsHandleCreated) return;
        restartingEngine=true;
        try
        {
            if (running) StopEngine();
            await Task.Delay(75);
            await EnsureEngineRunningAsync();
        }
        finally { restartingEngine=false; }
    }

    private async Task StartEngineAsync()
    {
        if(!OperatingSystem.IsWindowsVersionAtLeast(10,0,19041)){MessageBox.Show("Program capture requires Windows 10 build 19041 or newer.");return;}
        if(processBox.SelectedItem is not ProcessItem proc){MessageBox.Show("Select a program first.");return;}
        if(micBox.SelectedItem is not DeviceItem mic){MessageBox.Show("Select your regular microphone.");return;}
        if(outputBox.SelectedItem is not DeviceItem output){MessageBox.Show("Select CABLE Input. If it is missing, enable/install VB-CABLE.");return;}
        try
        {
             statusLabel.Text="● STARTING…"; statusLabel.ForeColor=Color.DarkOrange;
            micBuffer=new BufferedWaveProvider(AudioFormat){DiscardOnBufferOverflow=true,ReadFully=true}; programBuffer=new BufferedWaveProvider(AudioFormat){DiscardOnBufferOverflow=true,ReadFully=true};
            mixer=new TwoInputFloatMixer(micBuffer,programBuffer,AudioFormat); UpdateGains();
            player=new WasapiPlayerBuilder().WithDevice(output.Device).WithEventSync().Build(); player.Init(mixer); player.Play();
            micRecorder=new WasapiRecorderBuilder().WithDevice(mic.Device).WithSharedMode().WithEventSync().WithBufferLength(50).WithFormat(AudioFormat).Build();
            micRecorder.DataAvailable+=MicData; micRecorder.RecordingStopped+=Stopped; micRecorder.StartRecording();
            programRecorder=await new WasapiRecorderBuilder().WithProcessLoopback((uint)proc.Pid,ProcessLoopbackMode.IncludeTargetProcessTree).WithEventSync().WithBufferLength(50).WithFormat(AudioFormat).BuildAsync();
            programRecorder.DataAvailable+=ProgramData; programRecorder.RecordingStopped+=Stopped; programRecorder.StartRecording();
            running=true; programEnabled=false; UpdateGains(); UpdateUi(proc.DisplayName);
        }
        catch(Exception ex){StopEngine();MessageBox.Show("Could not start ProgramMic.\r\n\r\n"+ex.Message,"ProgramMic",MessageBoxButtons.OK,MessageBoxIcon.Error);}
        finally{}
    }

    private void MicData(ReadOnlySpan<byte> data,AudioClientBufferFlags flags,long pos,long qpc)=>WritePacket(micBuffer,data,flags);
    private void ProgramData(ReadOnlySpan<byte> data,AudioClientBufferFlags flags,long pos,long qpc)=>WritePacket(programBuffer,data,flags);
    private static void WritePacket(BufferedWaveProvider? b,ReadOnlySpan<byte> data,AudioClientBufferFlags flags){if(b==null||data.Length==0)return;if((flags&AudioClientBufferFlags.Silent)!=0)b.AddSamples(new byte[data.Length]);else b.AddSamples(data);}
    private void Stopped(object? sender,StoppedEventArgs e){if(e.Exception==null||closing)return;try{BeginInvoke(()=>{if(running){MessageBox.Show("Audio capture stopped:\r\n\r\n"+e.Exception.Message);StopEngine();}});}catch{}}

    private void ToggleProgram(){if(!running)return;programEnabled=!programEnabled;programBuffer?.ClearBuffer();UpdateGains();UpdateToggleUi();}
    private void UpdateGains(){if(mixer==null)return;mixer.MicGain=micVolume.Value/100f;mixer.ProgramGain=programEnabled?programVolume.Value/100f:0f;mixer.MasterGain=masterVolume.Value/100f;}
    private void UpdateUi(string proc)
    {
        // The audio engine now stays live so the regular microphone can remain routed
        // continuously. Program audio ON/OFF only changes ProgramGain, so the device
        // selectors must remain usable while the engine is running.
        processBox.Enabled=true;
        micBox.Enabled=true;
        outputBox.Enabled=true;
        refreshButton.Enabled=true;
        toggleButton.Enabled=true;
        UpdateToggleUi();
    }
    private void UpdateToggleUi()
    {
        if(!running){toggleButton.Text="PROGRAM AUDIO: OFF";toggleButton.BackColor=Panel;toggleButton.ForeColor=Color.White;return;}
        toggleButton.Text=$"PROGRAM AUDIO: {(programEnabled?"ON":"OFF")}   ({HotkeyText()})"; toggleButton.BackColor=programEnabled?Color.ForestGreen:SystemColors.Control; toggleButton.ForeColor=programEnabled?Color.White:SystemColors.ControlText;
        statusLabel.Text=programEnabled?"● MIC LIVE + PROGRAM AUDIO LIVE":"● MIC LIVE — program audio muted";statusLabel.ForeColor=PinkBright;
    }

    private void StopEngine()
    {
        running=false;programEnabled=false;
        try{if(programRecorder!=null){programRecorder.DataAvailable-=ProgramData;programRecorder.RecordingStopped-=Stopped;programRecorder.StopRecording();programRecorder.Dispose();}}catch{} programRecorder=null;
        try{if(micRecorder!=null){micRecorder.DataAvailable-=MicData;micRecorder.RecordingStopped-=Stopped;micRecorder.StopRecording();micRecorder.Dispose();}}catch{} micRecorder=null;
        try{player?.Stop();player?.Dispose();}catch{} player=null;micBuffer=null;programBuffer=null;mixer=null;
        if(!IsDisposed&&!closing){processBox.Enabled=micBox.Enabled=outputBox.Enabled=refreshButton.Enabled=true;toggleButton.Enabled=true;toggleButton.Text="PROGRAM AUDIO: OFF";toggleButton.BackColor=Panel;toggleButton.ForeColor=Color.White;statusLabel.Text="● READY — microphone routing starts automatically";statusLabel.ForeColor=TextSecondary;}
    }

    private void BeginAssignHotkey()
    {
        assigningHotkey=true;

        // Temporarily release the current global hotkey while waiting for the
        // replacement. The very next keyboard key pressed becomes the new hotkey.
        UnregisterHotKey(Handle,HOTKEY_ID);

        hotkeyLabel.Text="PRESS ANY KEY...";
        hotkeyLabel.ForeColor=PinkBright;
        setHotkeyButton.Text="PRESS A KEY";
        toggleButton.Enabled=false;

        // Keep keyboard focus inside ProgramMic so ProcessCmdKey receives the
        // next key even if a ComboBox or Button previously had focus.
        ActiveControl=null;
        Focus();
    }
    private bool RegisterCurrentHotkey(bool showError){if(!IsHandleCreated)return false;bool ok=RegisterHotKey(Handle,HOTKEY_ID,(uint)(hotkeyMods|Mods.NoRepeat),(uint)hotkeyKey);if(!ok&&showError)MessageBox.Show($"Windows could not register {HotkeyText()}. Another app may already use it.","ProgramMic Hotkey");return ok;}
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if(assigningHotkey)
        {
            // Strip modifier flags and use the actual first key pressed.
            Keys key=keyData & Keys.KeyCode;

            // Ignore modifier-only presses; wait for the first real key.
            if(key==Keys.ControlKey || key==Keys.ShiftKey || key==Keys.Menu ||
               key==Keys.LWin || key==Keys.RWin || key==Keys.None)
                return true;

            hotkeyKey=key;
            assigningHotkey=false;

            hotkeyLabel.Text=hotkeyKey.ToString();
            hotkeyLabel.ForeColor=TextSecondary;
            setHotkeyButton.Text="ASSIGN HOTKEY";
            toggleButton.Enabled=true;

            RegisterCurrentHotkey(true);
            UpdateToggleUi();
            SaveSettings();

            // Consume the assignment keystroke so it does not also activate a
            // focused control or immediately toggle Program Audio.
            return true;
        }

        return base.ProcessCmdKey(ref msg,keyData);
    }

    protected override void WndProc(ref Message m)
    {
        if(m.Msg==WM_HOTKEY && m.WParam.ToInt32()==HOTKEY_ID)
        {
            if(running) ToggleProgram();
            else BeginInvoke(async () =>
            {
                await EnsureEngineRunningAsync();
                if(running) ToggleProgram();
            });
            return;
        }
        base.WndProc(ref m);
    }
    private string HotkeyText(){var x=new List<string>();if(hotkeyMods.HasFlag(Mods.Control))x.Add("Ctrl");if(hotkeyMods.HasFlag(Mods.Shift))x.Add("Shift");if(hotkeyMods.HasFlag(Mods.Alt))x.Add("Alt");x.Add(hotkeyKey.ToString());return string.Join("+",x);}
    private void OnClosing(object? s,FormClosingEventArgs e){closing=true;refreshTimer.Stop();if(IsHandleCreated)UnregisterHotKey(Handle,HOTKEY_ID);StopEngine();}

    private sealed class ProcessItem
    {
        public int Pid{get;}
        public string ProcessName{get;}
        public string DisplayName{get;}

        public ProcessItem(int pid,string name,string title)
        {
            Pid=pid;
            ProcessName=name;
            DisplayName=string.IsNullOrWhiteSpace(title)?$"{name} (PID {pid})":$"{name} — {title} (PID {pid})";
        }

        public override string ToString()=>DisplayName;
    }

    private sealed class AppSettings
    {
        public string? ProcessName{get;set;}
        public string? MicrophoneDeviceId{get;set;}
        public string? OutputDeviceId{get;set;}
        public int ProgramVolume{get;set;}=100;
        public int MicVolume{get;set;}=100;
        public int OutputVolume{get;set;}=100;
        public int HotkeyKey{get;set;}=(int)Keys.F8;
        public uint HotkeyModifiers{get;set;}
    }
    private sealed class PinkSlider : Control
    {
        private int minimum=0, maximum=100, value=0, tickFrequency=10;
        private bool dragging;

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public int Minimum
        {
            get=>minimum;
            set { minimum=value; if(maximum<=minimum) maximum=minimum+1; Value=this.value; Invalidate(); }
        }
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public int Maximum
        {
            get=>maximum;
            set { maximum=Math.Max(value,minimum+1); Value=this.value; Invalidate(); }
        }
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public int Value
        {
            get=>value;
            set
            {
                int next=Math.Clamp(value,minimum,maximum);
                if(this.value==next) return;
                this.value=next;
                Invalidate();
                ValueChanged?.Invoke(this,EventArgs.Empty);
            }
        }
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public int TickFrequency
        {
            get=>tickFrequency;
            set { tickFrequency=Math.Max(1,value); Invalidate(); }
        }

        public event EventHandler? ValueChanged;

        public PinkSlider()
        {
            DoubleBuffered=true;
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
            Height=34;
            Cursor=Cursors.Hand;
            TabStop=true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g=e.Graphics;
            g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int left=7, right=Math.Max(left+1,ClientSize.Width-7);
            int lineY=10;
            using var linePen=new Pen(Color.FromArgb(225,225,230),3f);
            g.DrawLine(linePen,left,lineY,right,lineY);

            int range=Math.Max(1,maximum-minimum);
            float pct=(value-minimum)/(float)range;
            int x=left+(int)Math.Round((right-left)*pct);

            // Pink active portion and thumb, matching ProgramMic's accent.
            using var activePen=new Pen(Pink,3f);
            g.DrawLine(activePen,left,lineY,x,lineY);
            using var thumbBrush=new SolidBrush(Pink);
            g.FillRectangle(thumbBrush,x-5,lineY-7,10,15);

            using var tickPen=new Pen(Color.FromArgb(205,205,215),1f);
            for(int v=minimum;v<=maximum;v+=tickFrequency)
            {
                float tp=(v-minimum)/(float)range;
                int tx=left+(int)Math.Round((right-left)*tp);
                g.DrawLine(tickPen,tx,25,tx,28);
            }
            if((maximum-minimum)%tickFrequency!=0)
                g.DrawLine(tickPen,right,25,right,28);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if(e.Button!=MouseButtons.Left) return;
            dragging=true;
            Capture=true;
            SetValueFromX(e.X);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if(dragging) SetValueFromX(e.X);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if(e.Button!=MouseButtons.Left) return;
            dragging=false;
            Capture=false;
            SetValueFromX(e.X);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if(e.KeyCode==Keys.Left || e.KeyCode==Keys.Down) { Value--; e.Handled=true; }
            if(e.KeyCode==Keys.Right || e.KeyCode==Keys.Up) { Value++; e.Handled=true; }
            if(e.KeyCode==Keys.Home) { Value=Minimum; e.Handled=true; }
            if(e.KeyCode==Keys.End) { Value=Maximum; e.Handled=true; }
        }

        private void SetValueFromX(int mouseX)
        {
            int left=7, right=Math.Max(left+1,ClientSize.Width-7);
            float pct=Math.Clamp((mouseX-left)/(float)(right-left),0f,1f);
            Value=minimum+(int)Math.Round((maximum-minimum)*pct);
        }
    }

    private sealed class DeviceItem{public MMDevice Device{get;}public string Id=>Device.ID;public DeviceItem(MMDevice d){Device=d;}public override string ToString()=>Device.FriendlyName;}

    private sealed class TwoInputFloatMixer:IWaveProvider
    {
        private readonly IWaveProvider mic,program;private byte[] a=[],b=[];public float MicGain{get;set;}=1f;public float ProgramGain{get;set;}=0f;public float MasterGain{get;set;}=1f;public WaveFormat WaveFormat{get;}
        public TwoInputFloatMixer(IWaveProvider mic,IWaveProvider program,WaveFormat format){this.mic=mic;this.program=program;WaveFormat=format;if(format.Encoding!=WaveFormatEncoding.IeeeFloat||format.BitsPerSample!=32)throw new ArgumentException("Mixer requires 32-bit float audio.");}
        public int Read(Span<byte> dst){Ensure(dst.Length);var sa=a.AsSpan(0,dst.Length);var sb=b.AsSpan(0,dst.Length);sa.Clear();sb.Clear();mic.Read(sa);program.Read(sb);int n=dst.Length-dst.Length%4;var o=MemoryMarshal.Cast<byte,float>(dst[..n]);var ma=MemoryMarshal.Cast<byte,float>(sa[..n]);var pb=MemoryMarshal.Cast<byte,float>(sb[..n]);float mg=MicGain,pg=ProgramGain,master=MasterGain;for(int i=0;i<o.Length;i++)o[i]=Math.Clamp((ma[i]*mg+pb[i]*pg)*master,-1f,1f);if(n<dst.Length)dst[n..].Clear();return dst.Length;}
        private void Ensure(int n){if(a.Length<n)a=new byte[n];if(b.Length<n)b=new byte[n];}
    }
}
