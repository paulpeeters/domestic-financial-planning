#ifndef ProjectRoot
#define ProjectRoot "..\.."
#endif

#ifndef SourceDir
#define SourceDir "..\..\artifacts\publish\Desktop"
#endif

#ifndef OutputDir
#define OutputDir "..\..\artifacts\installer"
#endif

#ifndef AppVersion
#define AppVersion "1.0.3.0"
#endif

[Setup]
AppId={{D07CE113-42E1-48F7-8AC5-9C5EA3F93713}
AppName=Domestic Financial Planning
AppVersion={#AppVersion}
AppVerName=Domestic Financial Planning {#AppVersion}
AppPublisher=PWARE
DefaultDirName={localappdata}\Programs\Domestic Financial Planning
DefaultGroupName=Domestic Financial Planning
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir={#OutputDir}
OutputBaseFilename=DomesticFinancialPlanning-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile={#ProjectRoot}\FinancialPlanningApp.Web\wwwroot\favicon.ico
UninstallDisplayIcon={app}\FinancialPlanningApp.Web.exe
LicenseFile={#ProjectRoot}\LICENSE
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "dutch"; MessagesFile: "compiler:Languages\Dutch.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "secrets.json,secrets.*.json,appsettings.Development.json,appsettings.Local.json,*.pdb,*.map"
Source: "{#ProjectRoot}\LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Domestic Financial Planning"; Filename: "{app}\FinancialPlanningApp.Web.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\Domestic Financial Planning"; Filename: "{app}\FinancialPlanningApp.Web.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\FinancialPlanningApp.Web.exe"; Description: "Domestic Financial Planning starten"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

[Code]
procedure StopRunningDesktopApp();
var
  ResultCode: Integer;
begin
  Exec(
    ExpandConstant('{sys}\taskkill.exe'),
    '/IM FinancialPlanningApp.Web.exe /T /F',
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopRunningDesktopApp();
  Sleep(1000);
  Result := '';
end;
