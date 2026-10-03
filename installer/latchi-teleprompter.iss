; LATCHI TELEPROMPTER — Inno Setup script
; Per-user install (NO admin rights), x64 only, user data is NEVER touched
; on uninstall (stories/settings live in %APPDATA%\LATCHI Teleprompter).
;
; Build:  ISCC /DMyAppVersion=0.1.0 installer\latchi-teleprompter.iss
; Expects the self-contained publish output in ..\publish\

#define MyAppName "LATCHI TELEPROMPTER"
#define MyAppExeName "LATCHI-Teleprompter.exe"
#ifndef MyAppVersion
#define MyAppVersion "0.1.0"
#endif

[Setup]
AppId={{8F3A2C1E-6B7D-4E9A-9C41-LATCHITP01}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher=LATCHI
AppPublisherURL=https://github.com/latchidz/latchi-teleprompter
DefaultDirName={localappdata}\LATCHI Teleprompter
DefaultGroupName=LATCHI Teleprompter
DisableProgramGroupPage=yes
; per-user install: no UAC prompt, no admin needed
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
; x64 only
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=Output
OutputBaseFilename=LATCHI-Teleprompter-Setup-{#MyAppVersion}
SetupIconFile=..\assets\icon\latchi-teleprompter.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional icons:"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

; NOTE (deliberate): no [UninstallDelete] section — user stories, autosave and
; settings under %APPDATA%\LATCHI Teleprompter must survive an uninstall.
