; USBAnywhere Inno Setup Script
; Copyright (c) 2025-2026 SnakeFoxu (OscarLosu)

#define MyAppName "USBAnywhere"
#ifndef MyAppVersion
#define MyAppVersion "3.6.0"
#endif
#define MyAppPublisher "SnakeFoxu (OscarLosu)"
#define MyAppURL "https://github.com/oscarlosu-lab/USBAnywhere"
#define MyAppExeName "USBAnywhere.exe"

[Setup]
AppId={{7D9E4B2A-3C81-4F6E-9A1D-8E5B2C7F4A90}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=LICENSE
OutputDir=installer
OutputBaseFilename=USBAnywhere_Setup_v{#MyAppVersion}
SetupIconFile=icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
AppMutex=USBAnywhere_SingleInstance_Mutex
CloseApplications=yes
CloseApplicationsFilter=*.exe,*.dll
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
UsedUserAreasWarning=no
; WizardImageFile and WizardSmallImageFile removed - using Inno Setup defaults

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupicon"; Description: "Iniciar con Windows"; GroupDescription: "Opciones adicionales:"; Flags: unchecked
Name: "installdrivers"; Description: "Instalar drivers USB/IP (requiere reinicio)"; GroupDescription: "Configuración de drivers:"; Flags: unchecked

[Files]
Source: "Portable\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "Portable\usb.ids"; DestDir: "{app}"; Flags: ignoreversion
Source: "Portable\usbipw.exe"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "Portable\devnode.exe"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "Portable\libusbip.dll"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "Portable\resources.dll"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "Portable\CleanDrivers.ps1"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "Portable\drivers\*"; DestDir: "{app}\drivers"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "Portable\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "Portable\README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"" --minimized"; Flags: uninsdeletevalue; Tasks: startupicon

[Run]
; Install USB/IP drivers if selected (remove any previous UDE node first to prevent duplicates)
; Skip usbip2_filter on Windows To Go / USB-booted Windows (PortableOperatingSystem != 0) to prevent boot-hub veto BSOD
Filename: "pnputil.exe"; Parameters: "/add-driver ""{app}\drivers\usbip2_filter.inf"" /install"; StatusMsg: "Instalando filtro USB/IP..."; Tasks: installdrivers; Check: IsNotPortableOS; Flags: runhidden waituntilterminated
Filename: "{app}\devnode.exe"; Parameters: "remove ROOT\USBIP_WIN2\UDE root"; StatusMsg: "Limpiando nodos virtuales previos..."; Tasks: installdrivers; Flags: runhidden waituntilterminated skipifdoesntexist
Filename: "{app}\devnode.exe"; Parameters: "install ""{app}\drivers\usbip2_ude.inf"" ROOT\USBIP_WIN2\UDE"; StatusMsg: "Instalando controlador emulado USB/IP (UDE)..."; Tasks: installdrivers; Flags: runhidden waituntilterminated skipifdoesntexist
; Launch application
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent runascurrentuser

[UninstallRun]
; Ensure tray process is terminated and clean up UDE device node before deleting files
Filename: "taskkill.exe"; Parameters: "/F /IM {#MyAppExeName}"; Flags: runhidden waituntilterminated; RunOnceId: "StopUSBAnywhere"
Filename: "{app}\devnode.exe"; Parameters: "remove ROOT\USBIP_WIN2\UDE root"; Flags: runhidden waituntilterminated skipifdoesntexist; RunOnceId: "RemoveUdeNode"

[UninstallDelete]
Type: filesandordirs; Name: "{app}\logs"
Type: filesandordirs; Name: "{app}\temp"
Type: filesandordirs; Name: "{app}\data"

[Code]
function IsNotPortableOS: Boolean;
var
  PortableVal: Cardinal;
begin
  Result := True;
  if RegQueryDWordValue(HKEY_LOCAL_MACHINE, 'SYSTEM\CurrentControlSet\Control', 'PortableOperatingSystem', PortableVal) then
  begin
    if PortableVal <> 0 then
      Result := False;
  end;
end;

procedure CleanupLegacyClientKey(RootKey: Integer);
var
  LegacyKey, DisplayName, OldDir, OldExe, CurDir: String;
  ResultCode: Integer;
begin
  LegacyKey := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{B8F3D2A1-5E7C-4D9A-8B2E-1F6C3A9D0E5B}_is1';
  if RegQueryStringValue(RootKey, LegacyKey, 'DisplayName', DisplayName) then
  begin
    if CompareText(Trim(DisplayName), 'USBAnywhere') = 0 then
    begin
      if RegQueryStringValue(RootKey, LegacyKey, 'InstallLocation', OldDir) then
      begin
        OldDir := RemoveBackslashUnlessRoot(Trim(OldDir));
        CurDir := RemoveBackslashUnlessRoot(ExpandConstant('{app}'));
        if (OldDir <> '') and (CompareText(OldDir, CurDir) <> 0) then
        begin
          OldExe := AddBackslash(OldDir) + 'USBAnywhere.exe';
          if FileExists(OldExe) then
          begin
            Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM USBAnywhere.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
            DeleteFile(OldExe);
          end;
        end;
      end;
      RegDeleteKeyIncludingSubkeys(RootKey, LegacyKey);
    end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  RunVal: String;
begin
  if CurStep = ssPostInstall then
  begin
    CleanupLegacyClientKey(HKLM64);
    CleanupLegacyClientKey(HKLM32);
    CleanupLegacyClientKey(HKCU);
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', '{#MyAppName}', RunVal) then
      RegWriteStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', '{#MyAppName}', '"' + ExpandConstant('{app}\{#MyAppExeName}') + '" --minimized');
  end;
end;
