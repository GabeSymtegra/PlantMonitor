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

[Icons]
Name: "{autoprograms}\PlantMonitor"; Filename: "{app}\backend.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\PlantMonitor"; Filename: "{app}\backend.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\backend.exe"; Description: "Launch PlantMonitor backend"; Flags: nowait postinstall skipifsilent