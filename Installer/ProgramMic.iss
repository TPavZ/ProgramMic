#define MyAppName "ProgramMic"
#define MyAppVersion "2.0.0-dev"
#define MyAppPublisher "tpavz"
#define MyAppExeName "ProgramMic.exe"
#define VBCableUrl "https://vb-audio.com/Cable/"

[Setup]
AppId={{7D6754A8-CCF8-45E8-A1B4-30AF35A995C1}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\ProgramMic
DefaultGroupName=ProgramMic
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=ProgramMic-Setup-v{#MyAppVersion}
SetupIconFile=..\ProgramMic.ico
UninstallDisplayIcon={app}\ProgramMic.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
VersionInfoVersion=2.0.0.0
VersionInfoProductName=ProgramMic
VersionInfoProductVersion=2.0.0-dev
VersionInfoCompany=tpavz

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\ProgramMic"; Filename: "{app}\ProgramMic.exe"
Name: "{autodesktop}\ProgramMic"; Filename: "{app}\ProgramMic.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "ProgramMic"; ValueData: """{app}\ProgramMic.exe"" --tray"; Flags: uninsdeletevalue

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Run]
Filename: "{app}\ProgramMic.exe"; Description: "Launch ProgramMic"; Flags: nowait postinstall skipifsilent

[Code]
var
  DependencyPage: TWizardPage;
  InfoLabel: TNewStaticText;
  StatusLabel: TNewStaticText;
  DownloadButton: TNewButton;
  RecheckButton: TNewButton;

function IsVBCableInstalled(): Boolean;
var
  ResultCode: Integer;
  PSArgs: String;
begin
  Result := False;

  { Fast fallback for classic VB-CABLE driver packages. }
  if FileExists(ExpandConstant('{sys}\drivers\vbaudio_cable64_win10.sys')) or
     FileExists(ExpandConstant('{sys}\drivers\vbaudio_cable64_win7.sys')) or
     FileExists(ExpandConstant('{sys}\drivers\vbaudio_cable64_vista.sys')) or
     FileExists(ExpandConstant('{sys}\drivers\vbaudio_cable_win10.sys')) or
     FileExists(ExpandConstant('{sys}\drivers\vbaudio_cable_win7.sys')) or
     FileExists(ExpandConstant('{sys}\drivers\vbaudio_cable_vista.sys')) then
  begin
    Result := True;
    Exit;
  end;

  { Primary Windows 10/11 check: ask Plug-and-Play for an active VB-CABLE
    audio endpoint. PowerShell exits 0 only when a matching device exists. }
  PSArgs :=
    '-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "' +
    '$d = Get-PnpDevice -PresentOnly -ErrorAction SilentlyContinue | ' +
    'Where-Object { ($_.FriendlyName -match ''CABLE (Input|Output)'') -and ' +
    '(($_.Manufacturer -match ''VB-Audio'') -or ($_.FriendlyName -match ''VB-Audio'')) }; ' +
    'if ($d) { exit 0 } else { exit 1 }"';

  if Exec(
       ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
       PSArgs, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    Result := (ResultCode = 0);
end;

procedure UpdateDependencyState();
begin
  if IsVBCableInstalled() then
  begin
    StatusLabel.Caption := 'VB-CABLE detected - ready to continue.';
    StatusLabel.Font.Color := clGreen;
    WizardForm.NextButton.Enabled := True;
  end
  else
  begin
    StatusLabel.Caption := 'VB-CABLE not detected.';
    StatusLabel.Font.Color := clRed;
    WizardForm.NextButton.Enabled := False;
  end;
end;

procedure OpenVBCableDownload(Sender: TObject);
var
  ErrorCode: Integer;
begin
  ShellExec('open', '{#VBCableUrl}', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
end;

procedure RecheckVBCable(Sender: TObject);
begin
  UpdateDependencyState();

  if IsVBCableInstalled() then
    MsgBox('VB-CABLE detected. Click Next to continue installing ProgramMic.', mbInformation, MB_OK)
  else
    MsgBox(
      'VB-CABLE is required before ProgramMic can be installed.' + Chr(13) + Chr(10) + Chr(13) + Chr(10) +
      'Install VB-CABLE, restart Windows if requested, then click Recheck.',
      mbError, MB_OK);
end;

procedure InitializeWizard();
begin
  DependencyPage := CreateCustomPage(
    wpWelcome,
    'VB-CABLE Required',
    'ProgramMic uses VB-Audio Virtual Cable as its virtual microphone destination.');

  InfoLabel := TNewStaticText.Create(DependencyPage);
  InfoLabel.Parent := DependencyPage.Surface;
  InfoLabel.Left := 0;
  InfoLabel.Top := 8;
  InfoLabel.Width := DependencyPage.SurfaceWidth;
  InfoLabel.Height := 66;
  InfoLabel.AutoSize := False;
  InfoLabel.WordWrap := True;
  InfoLabel.Caption :=
    'ProgramMic requires VB-CABLE. If it is already installed, Setup will detect it automatically.' +
    Chr(13) + Chr(10) + Chr(13) + Chr(10) +
    'Otherwise, download and install VB-CABLE, then return here and click Recheck.';

  StatusLabel := TNewStaticText.Create(DependencyPage);
  StatusLabel.Parent := DependencyPage.Surface;
  StatusLabel.Left := 0;
  StatusLabel.Top := 80;
  StatusLabel.Width := DependencyPage.SurfaceWidth;
  StatusLabel.AutoSize := False;
  StatusLabel.Caption := 'Checking for VB-CABLE...';

  DownloadButton := TNewButton.Create(DependencyPage);
  DownloadButton.Parent := DependencyPage.Surface;
  DownloadButton.Left := 0;
  DownloadButton.Top := 112;
  DownloadButton.Width := 175;
  DownloadButton.Caption := 'Download VB-CABLE';
  DownloadButton.OnClick := @OpenVBCableDownload;

  RecheckButton := TNewButton.Create(DependencyPage);
  RecheckButton.Parent := DependencyPage.Surface;
  RecheckButton.Left := 190;
  RecheckButton.Top := 112;
  RecheckButton.Width := 135;
  RecheckButton.Caption := 'Recheck';
  RecheckButton.OnClick := @RecheckVBCable;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = DependencyPage.ID then
    UpdateDependencyState();
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;

  if CurPageID = DependencyPage.ID then
  begin
    UpdateDependencyState();

    if not IsVBCableInstalled() then
    begin
      MsgBox(
        'VB-CABLE is required to install ProgramMic.' + Chr(13) + Chr(10) + Chr(13) + Chr(10) +
        'Use Download VB-CABLE, install the driver, then click Recheck.',
        mbError, MB_OK);
      Result := False;
    end;
  end;
end;
