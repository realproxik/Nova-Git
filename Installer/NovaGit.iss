; Build with: ISCC Installer\NovaGit.iss
; First run: powershell -ExecutionPolicy Bypass -File Installer\Build-Installer.ps1

#ifndef MyAppName
  #define MyAppName "NovaGit"
#endif
#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#ifndef MyAppPublisher
  #define MyAppPublisher "Jatin Kaushik"
#endif
#ifndef MyAppExeName
  #define MyAppExeName "NovaGit.exe"
#endif

[Setup]
AppId={{A5E58858-4D4C-48D2-ACAC-AEC5C58F0C19}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE.txt
InfoBeforeFile=rules.txt
OutputDir=..\artifacts\installer
OutputBaseFilename=NovaGit-Setup-{#MyAppVersion}
SetupIconFile=..\assets\novagit.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ChangesEnvironment=yes
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop icon"; GroupDescription: "Additional shortcuts:"; Flags: unchecked
Name: "addtopath"; Description: "Add NovaGit to your user &PATH"; GroupDescription: "Command-line options:"; Flags: unchecked

[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch NovaGit"; Flags: nowait postinstall skipifsilent

[Code]
procedure AddToUserPath;
var
  CurrentPath: String;
begin
  if RegQueryStringValue(HKCU, 'Environment', 'Path', CurrentPath) then begin
    if Pos(Uppercase(ExpandConstant('{app}')), Uppercase(CurrentPath)) = 0 then
      RegWriteStringValue(HKCU, 'Environment', 'Path', CurrentPath + ';' + ExpandConstant('{app}'));
  end else
    RegWriteStringValue(HKCU, 'Environment', 'Path', ExpandConstant('{app}'));
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and WizardIsTaskSelected('addtopath') then
    AddToUserPath;
end;
