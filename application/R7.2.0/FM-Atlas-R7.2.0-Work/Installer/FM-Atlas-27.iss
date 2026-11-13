#define MyAppName "FM Atlas 27"
#define MyAppVersion "7.2.0"
#define MyAppPublisher "FM Atlas"
#define MyAppExeName "FmAtlas.exe"

[Setup]
AppId={{0D85D37A-DBBD-4EB2-9F13-4D7D51EA5A10}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\FM Atlas 27
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
OutputDir=..\installer-output
OutputBaseFilename=FM-Atlas-27-Setup-7.2.0
SetupIconFile=..\src\FmAtlas\Assets\FmAtlas.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch FM Atlas 27"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Intentionally do not delete %LOCALAPPDATA%\FmAtlas: it contains the user's database and imports.
