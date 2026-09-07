; =========================================================================
; MR Print Hub - Complete Inno Setup Production Script
; Generates a single standalone installer: MRPrintHub-Setup.exe
; =========================================================================

#define MyAppName "MR Print Hub"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "MR Print Hub Team"
#define MyAppURL "https://github.com/mrprinthub"
#define MyAppExeName "MRPrintHub.Desktop.exe"
#define MyServiceExeName "MRPrintHub.Service.exe"

[Setup]
; --- Application Metadata ---
AppId={{D37E8443-41BF-4E90-953B-2A0D84126B44}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} v{#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}

; --- Installation Directory & Mode ---
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible

; --- Output Artifact Settings ---
OutputDir=..\..\dist
OutputBaseFilename=MRPrintHub-Setup
SetupIconFile=MRPrintHub.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

; --- Compression & Performance ---
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes

; --- Wizard Interface & UX ---
WizardStyle=modern
WizardSizePercent=105
DisableWelcomePage=no
CloseApplications=force
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce
Name: "startupicon"; Description: "Launch MR Print Hub Dashboard automatically on Windows startup"; GroupDescription: "System Startup:"; Flags: checkedonce

[Files]
; --- Core Application Binaries (from published artifacts) ---
; Note: Place published files in .\publish\ before compiling, or run build-installer.ps1
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; --- Supporting Automation Scripts ---
Source: "Scripts\setup-dependencies.ps1"; DestDir: "{app}\Scripts"; Flags: ignoreversion
Source: "Scripts\uninstall-cleanup.ps1"; DestDir: "{app}\Scripts"; Flags: ignoreversion

[Dirs]
; --- Data & Storage Folders (ProgramData) with Full Access for All Users ---
Name: "{commonappdata}\MRPrintHub"; Permissions: users-full everyone-full
Name: "{commonappdata}\MRPrintHub\Received"; Permissions: users-full everyone-full
Name: "{commonappdata}\MRPrintHub\Temp"; Permissions: users-full everyone-full
Name: "{commonappdata}\MRPrintHub\Logs"; Permissions: users-full everyone-full
Name: "{commonappdata}\MRPrintHub\Database"; Permissions: users-full everyone-full

[Icons]
; --- Start Menu Shortcuts ---
Name: "{autoprograms}\{#MyAppName}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Comment: "MR Print Hub Dashboard"
Name: "{autoprograms}\{#MyAppName}\Received Files Folder"; Filename: "{commonappdata}\MRPrintHub\Received"; Comment: "Open received customer uploads folder"
Name: "{autoprograms}\{#MyAppName}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"

; --- Optional Desktop & Startup Shortcuts ---
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon; Comment: "MR Print Hub Mobile-to-PC Print System"
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: startupicon

[Run]
; --- Step 1: Silent Background Execution of System Setup & Services ---
Filename: "powershell.exe"; \
    Parameters: "-ExecutionPolicy Bypass -NoProfile -WindowStyle Hidden -File ""{app}\Scripts\setup-dependencies.ps1"" -InstallDir ""{app}"" -Port 5000"; \
    StatusMsg: "Configuring Windows Service, Firewall rules, and runtime permissions..."; \
    Flags: runhidden waituntilterminated

; --- Step 2: Immediate Launch Option on Finish Page ---
Filename: "{app}\{#MyAppExeName}"; \
    Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; \
    Flags: nowait postinstall skipifsilent

[UninstallRun]
; --- Silent Cleanup of Windows Service & Firewall on Uninstall ---
Filename: "powershell.exe"; \
    Parameters: "-ExecutionPolicy Bypass -NoProfile -WindowStyle Hidden -File ""{app}\Scripts\uninstall-cleanup.ps1"" -ServiceName ""MR Print Hub Service"" -InstallDir ""{app}"""; \
    Flags: runhidden waituntilterminated

[UninstallDelete]
; Clean up temp files while preserving customer uploads in ProgramData\MRPrintHub\Received
Type: filesandordirs; Name: "{app}\Scripts"
Type: files; Name: "{app}\*.log"

[Code]
// Pascal Script for pre-install validation and running process checks
function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;
  // Stop existing running service or desktop app if already active
  Exec('powershell.exe', '-ExecutionPolicy Bypass -NoProfile -Command "Stop-Service -Name ''MR Print Hub Service'' -Force -ErrorAction SilentlyContinue; Stop-Process -Name ''MRPrintHub.Desktop'' -Force -ErrorAction SilentlyContinue"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function InitializeUninstall(): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;
  // Stop running instances before uninstallation begins
  Exec('powershell.exe', '-ExecutionPolicy Bypass -NoProfile -Command "Stop-Service -Name ''MR Print Hub Service'' -Force -ErrorAction SilentlyContinue; Stop-Process -Name ''MRPrintHub.Desktop'' -Force -ErrorAction SilentlyContinue"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;
