; PrayerBar Windows installer. Compile with Inno Setup 6.
#define AppName "PrayerBar"
#define AppVersion "1.0.0"
#define AppPublisher "PrayerBar"
#define AppExeName "PrayerBar.exe"
#define PublishDir "..\artifacts\installer-payload"

[Setup]
AppId={{D73E7A20-09B7-4F6D-94FA-1C66FAC29B02}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; Per-user install: no administrator prompt and HKCU startup stays tied to the signed-in user.
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
OutputDir=..\artifacts\installer
OutputBaseFilename=PrayerBar-Setup-{#AppVersion}-win-x64
SetupIconFile=..\src\PrayerBar.App\Resources\PrayerBar.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
VersionInfoVersion={#AppVersion}
VersionInfoProductName={#AppName}
VersionInfoDescription={#AppName} prayer times widget installer

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked
Name: "startup"; Description: "Start PrayerBar when I sign in"; GroupDescription: "Startup:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\VERIFICATION.md"; DestDir: "{app}\docs"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autoprograms}\{#AppName}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "PrayerBar"; ValueData: """{app}\{#AppExeName}"" --startup"; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch PrayerBar"; Flags: nowait postinstall skipifsilent
