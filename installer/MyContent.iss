#define AppName "My Content"
#define AppPublisher "Hamza Watfa"
#define AppExeName "MyContent.exe"

#ifndef AppVersion
  #define AppVersion "0.1.2"
#endif

[Setup]
AppId={{7B15A8E5-2AE3-4D1A-98A2-CF6A6A18D1C1}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppMutex=MyContent.SingleInstance
SetupMutex=MyContent.SetupMutex
DefaultDirName={localappdata}\Programs\MyContent
DefaultGroupName={#AppName}
OutputDir=artifacts
OutputBaseFilename=MyContent-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
CloseApplications=yes
RestartApplications=no
Uninstallable=yes
MinVersion=10.0
DisableProgramGroupPage=yes

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\My Content"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\My Content"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Parameters: "/updated"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifnotsilent
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent
