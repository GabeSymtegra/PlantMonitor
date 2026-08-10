#define SourceDir GetEnv("PM_SOURCE_DIR")
#define OutputDir GetEnv("PM_OUTPUT_DIR")

#if SourceDir == ""
  #define SourceDir "..\\artifacts\\release\\backend"
#endif

#if OutputDir == ""
  #define OutputDir "..\\artifacts\\installer"
#endif

[Setup]
AppId={{C3D24A87-9A0C-4F74-9A3B-E208D3FBBE73}
AppName=PlantMonitor
AppVersion=0.1.0-alpha
AppPublisher=PlantMonitor
DefaultDirName={autopf}\PlantMonitor
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=PlantMonitor-Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\scripts\install-lan-service.ps1"; DestDir: "{app}\scripts"; Flags: ignoreversion
Source: "..\scripts\test-lan-service.ps1"; DestDir: "{app}\scripts"; Flags: ignoreversion
Source: "..\scripts\uninstall-lan-service.ps1"; DestDir: "{app}\scripts"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\PlantMonitor"; Filename: "http://localhost:5050/"
Name: "{autodesktop}\PlantMonitor"; Filename: "http://localhost:5050/"; Tasks: desktopicon

[Run]
Filename: "powershell.exe"; Parameters: "-ExecutionPolicy Bypass -File ""{app}\scripts\install-lan-service.ps1"" -UseInstalledFiles -InstallDirectory ""{app}"" -BootstrapAdminPassword ""{code:GetBootstrapAdminPassword}"" -BootstrapViewerPassword ""{code:GetBootstrapViewerPassword}"""; Description: "Install and start PlantMonitor LAN service"; Flags: runhidden waituntilterminated postinstall skipifsilent
Filename: "http://localhost:5050/"; Description: "Open PlantMonitor"; Flags: postinstall skipifsilent unchecked

[Code]
var
  BootstrapAdminPasswordPage: TInputQueryWizardPage;

procedure InitializeWizard;
begin
  BootstrapAdminPasswordPage := CreateInputQueryPage(
    wpSelectTasks,
    'PlantMonitor LAN Service Setup',
    'Provide the first administrator password for the hosted LAN service.',
    'This password is used only when the installed database has no existing admin account.'
  );

  BootstrapAdminPasswordPage.Add('Bootstrap administrator password:', True);
  BootstrapAdminPasswordPage.Add('Viewer display password:', True);
end;

function GetBootstrapAdminPassword(Param: string): string;
begin
  if Assigned(BootstrapAdminPasswordPage) then
    Result := BootstrapAdminPasswordPage.Values[0]
  else
    Result := '';
end;

function GetBootstrapViewerPassword(Param: string): string;
begin
  if Assigned(BootstrapAdminPasswordPage) then
    Result := BootstrapAdminPasswordPage.Values[1]
  else
    Result := '';
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;

  if (Assigned(BootstrapAdminPasswordPage)) and (CurPageID = BootstrapAdminPasswordPage.ID) then
  begin
    if Trim(BootstrapAdminPasswordPage.Values[0]) = '' then
    begin
      MsgBox('Enter a bootstrap administrator password before continuing.', mbError, MB_OK);
      Result := False;
    end
    else if Trim(BootstrapAdminPasswordPage.Values[1]) = '' then
    begin
      MsgBox('Enter a Viewer display password before continuing.', mbError, MB_OK);
      Result := False;
    end;
  end;
end;