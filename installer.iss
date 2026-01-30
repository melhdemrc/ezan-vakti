[Setup]
AppName=Ezan Vakti
AppVersion=1.0.0
DefaultDirName={autopf}\EzanVakti
DefaultGroupName=EzanVakti
OutputBaseFilename=EzanVakti_Setup
Compression=lzma
SolidCompression=yes
PrivilegesRequired=admin
SetupIconFile=Assets\icon.ico
UninstallDisplayIcon={app}\EzanVakti.exe

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startup"; Description: "Windows açıldığında otomatik başlat"; GroupDescription: "Diğer Seçenekler:"; Flags: checkedonce

[Files]
Source: "publish\EzanVakti.exe"; DestDir: "{app}"; Flags: ignoreversion

Source: "publish\Assets\*"; DestDir: "{app}\Assets"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "publish\*.dll"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Ezan Vakti"; Filename: "{app}\EzanVakti.exe"
Name: "{commondesktop}\Ezan Vakti"; Filename: "{app}\EzanVakti.exe"; Tasks: desktopicon
Name: "{userstartup}\Ezan Vakti"; Filename: "{app}\EzanVakti.exe"; Tasks: startup

[Run]
Filename: "{app}\EzanVakti.exe"; Description: "{cm:LaunchProgram,Ezan Vakti}"; Flags: nowait postinstall skipifsilent
