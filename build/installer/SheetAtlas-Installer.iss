; ============================================
; SheetAtlas - Inno Setup Installer Script
; ============================================
; Creates a professional Windows installer with:
; - Self-contained .NET deployment
; - Start Menu shortcuts
; - Optional Desktop shortcut
; - Uninstaller registration
; - Code signing support
;
; Version can be passed from command line:
;   ISCC.exe /DMyAppVersion=0.4.0 SheetAtlas-Installer.iss

#define MyAppName "SheetAtlas"
; Version: passed via /DMyAppVersion=x.x.x from CI, fallback to 0.0.0-dev
#ifndef MyAppVersion
  #define MyAppVersion "0.0.0-dev"
#endif
#define MyAppPublisher "SheetAtlas"
#define MyAppURL "https://github.com/ghostintheshell-192/sheet-atlas"
#define MyAppExeName "SheetAtlas.UI.Avalonia.exe"
#define MyAppDescription "Cross-file Excel search and row comparison tool"

[Setup]
; Basic Information
AppId={{8E5C9A3B-2F4D-4E8A-9B1C-7D6E5F3A2B1C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
AppCopyright=Copyright (C) 2025 {#MyAppPublisher}
VersionInfoVersion={#MyAppVersion}
VersionInfoDescription={#MyAppDescription}

; Installation Directories
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes

; Output Configuration
OutputDir=..\output
OutputBaseFilename=SheetAtlas-Setup-{#MyAppVersion}-win-x64
SetupIconFile=..\..\assets\icons\app.ico
UninstallDisplayIcon={app}\app.ico

; Compression
Compression=lzma2/max
SolidCompression=yes
LZMANumBlockThreads=2

; Requirements
MinVersion=10.0.17763
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

; UI Configuration
WizardStyle=modern
DisableWelcomePage=no

; Privileges
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog

; Uninstall
UninstallDisplayName={#MyAppName}
UninstallFilesDir={app}\uninstall

; Licensing
LicenseFile=..\..\LICENSE

; Code Signing (conditional)
#ifndef NoSign
SignTool=default /d $q{#MyAppName} Installer$q /du $q{#MyAppURL}$q $f
SignedUninstaller=yes
#endif

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Main application and all dependencies
Source: "..\publish\windows-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Application icon
Source: "..\..\assets\icons\app.ico"; DestDir: "{app}"; Flags: ignoreversion
; License file
Source: "..\..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
; Documentation
Source: "..\..\README.md"; DestDir: "{app}"; Flags: ignoreversion isreadme

[Icons]
; Start Menu shortcut (always created)
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"; Comment: "{#MyAppDescription}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"; IconFilename: "{app}\app.ico"

; Desktop shortcut (optional)
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"; Tasks: desktopicon; Comment: "{#MyAppDescription}"

[Run]
; Option to launch application after installation
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Clean up any user-created files
Type: filesandordirs; Name: "{app}"

[Code]
// ============================================
// Custom Pascal Script Functions
// ============================================

// Helper function to uninstall previous version
function UninstallCurrentVersion(): Boolean;
var
  UninstallString: String;
  ResultCode: Integer;
begin
  Result := False;

  // Get uninstall string from registry
  if RegQueryStringValue(HKEY_LOCAL_MACHINE,
    'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{{8E5C9A3B-2F4D-4E8A-9B1C-7D6E5F3A2B1C}_is1',
    'UninstallString', UninstallString) then
  begin
    // Remove quotes from uninstall string
    UninstallString := RemoveQuotes(UninstallString);

    // Run uninstaller silently
    if Exec(UninstallString, '/VERYSILENT /NORESTART /SUPPRESSMSGBOXES', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    begin
      Result := (ResultCode = 0);
    end;
  end;
end;

// Setup initialization with upgrade detection
function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;

  // Check if .NET 8 Runtime is installed (self-contained, so this is informational only)
  // Could add version detection here if needed

  // Check for previous installation
  if RegKeyExists(HKEY_LOCAL_MACHINE, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{{8E5C9A3B-2F4D-4E8A-9B1C-7D6E5F3A2B1C}_is1') then
  begin
    if MsgBox('A previous version of SheetAtlas is installed. Do you want to uninstall it first?' + #13#10 + #13#10 +
              'Click Yes to uninstall (recommended)' + #13#10 +
              'Click No to cancel installation' + #13#10 +
              'Click Cancel to install anyway (may cause issues)',
              mbConfirmation, MB_YESNOCANCEL) = IDYES then
    begin
      // Uninstall previous version
      if not UninstallCurrentVersion() then
      begin
        MsgBox('Failed to uninstall previous version. Please uninstall manually first.', mbError, MB_OK);
        Result := False;
      end;
    end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // Perform any post-installation tasks here
    // Could add firewall rules, file associations, etc.
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // Cleanup after uninstall
    // Remove any remaining user data if desired
  end;
end;

function InitializeUninstall(): Boolean;
begin
  Result := True;

  // Check if application is running
  if CheckForMutexes('SheetAtlas-SingleInstance') then
  begin
    MsgBox('SheetAtlas is currently running. Please close it before uninstalling.', mbError, MB_OK);
    Result := False;
  end;
end;

// ============================================
// Custom Wizard Pages (optional)
// ============================================

procedure InitializeWizard();
begin
  // Could add custom wizard pages here
  // Example: configuration options, data migration, etc.
end;

// ============================================
// Custom Uninstall Messages
// ============================================

function UninstallNeedRestart(): Boolean;
begin
  // Return True if reboot is needed after uninstall
  Result := False;
end;

[Messages]
; Custom messages
WelcomeLabel2=This will install [name/ver] on your computer.%n%nSheetAtlas searches across multiple Excel files and compares rows between them, with 100% local processing for complete data privacy.%n%nIt is recommended that you close all other applications before continuing.
FinishedLabel=Setup has finished installing [name] on your computer.%n%nThe application may be launched by selecting the installed shortcuts.
