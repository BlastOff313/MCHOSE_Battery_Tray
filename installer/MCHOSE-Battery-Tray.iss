#define MyAppName "MCHOSE Battery Tray"
#define MyAppVersion "10.0.2"
#define MyAppPublisher "MCHOSE Battery Tray contributors"
#define MyAppExeName "MCHOSE.BatteryTray.exe"

[Setup]
AppId={{8D3A3D52-7B56-4A0F-9C8C-57B230C9B410}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppCopyright=MIT License
UninstallDisplayName={#MyAppName}
DefaultDirName={localappdata}\Programs\MCHOSE Battery Tray
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
OutputDir=..\dist
OutputBaseFilename=MCHOSE-Battery-Tray-Setup-{#MyAppVersion}
SetupIconFile=..\assets\MCHOSE-Battery-Tray.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
WizardStyle=modern
Compression=lzma2
SolidCompression=yes
CloseApplications=yes
RestartApplications=no
Uninstallable=yes
ChangesAssociations=no
VersionInfoVersion=10.0.2.0
VersionInfoProductName={#MyAppName}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription=Windows tray battery monitor for supported MCHOSE headsets

[Tasks]
Name: "autostart"; Description: "Start MCHOSE Battery Tray when I sign in to Windows"; Flags: unchecked

[Files]
Source: "..\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"

[Registry]
; Per-user startup, optional during setup. The app can also toggle this from its tray menu.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "MCHOSE_BatteryTray"; ValueData: """{app}\{#MyAppExeName}"" --background"; Tasks: autostart; Flags: uninsdeletevalue
; Remove the previous PowerShell launcher to prevent duplicate tray icons after upgrading.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "MCHOSE_X9_BatteryTray"; Flags: deletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: postinstall nowait skipifsilent

[UninstallRun]
; Also remove autostart if the user enabled it later from the tray menu.
Filename: "{sys}\reg.exe"; Parameters: "delete ""HKCU\Software\Microsoft\Windows\CurrentVersion\Run"" /v ""MCHOSE_BatteryTray"" /f"; Flags: runhidden
