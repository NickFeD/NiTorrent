#define MyAppName "NiTorrent"
#define MyAppPublisher "NickFeD"
#define MyAppExeName "NiTorrent.App.exe"
#define MyAppVersion GetEnv("APP_VERSION")
#define MySourceDir GetEnv("PUBLISH_DIR")
#define MyOutputDir GetEnv("INSTALLER_OUTPUT_DIR")

[Setup]
; ВАЖНО: AppId не менять после первого релиза.
; По нему Windows/Inno понимают, что это то же самое приложение при обновлении/удалении.
AppId={{9A89E2C6-684A-4A37-9F8B-7D2F6B39C411}

AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}

AppPublisherURL=https://github.com/NickFeD/NiTorrent
AppSupportURL=https://github.com/NickFeD/NiTorrent/issues
AppUpdatesURL=https://github.com/NickFeD/NiTorrent/releases

; Установка без прав администратора.
; Приложение ставится только для текущего пользователя.
PrivilegesRequired=lowest
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}

; Имя итогового установщика.
OutputDir={#MyOutputDir}
OutputBaseFilename=NiTorrent-Setup-v{#MyAppVersion}-x64

Compression=lzma2
SolidCompression=yes
WizardStyle=modern

ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

; Иконка в списке установленных приложений Windows.
UninstallDisplayIcon={app}\{#MyAppExeName}

; Сообщает Windows, что установщик меняет ассоциации файлов/протоколов.
ChangesAssociations=yes

; Лучше оставить включенным: Inno попытается закрыть приложение при uninstall/update.
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Создать ярлык на рабочем столе"; GroupDescription: "Дополнительно:"; Flags: unchecked

[Files]
; Кладём весь publish output внутрь {app}.
Source: "{#MySourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\NiTorrent"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\NiTorrent"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; ============================================================
; Регистрация NiTorrent в Windows Default Apps
; ============================================================

Root: HKCU; Subkey: "Software\NiTorrent\Capabilities"; ValueType: string; ValueName: "ApplicationName"; ValueData: "NiTorrent"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\NiTorrent\Capabilities"; ValueType: string; ValueName: "ApplicationDescription"; ValueData: "Torrent client for .torrent files and magnet links"; Flags: uninsdeletekey

Root: HKCU; Subkey: "Software\NiTorrent\Capabilities\FileAssociations"; ValueType: string; ValueName: ".torrent"; ValueData: "NiTorrent.TorrentFile"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\NiTorrent\Capabilities\UrlAssociations"; ValueType: string; ValueName: "magnet"; ValueData: "NiTorrent.Magnet"; Flags: uninsdeletekey

Root: HKCU; Subkey: "Software\RegisteredApplications"; ValueType: string; ValueName: "NiTorrent"; ValueData: "Software\NiTorrent\Capabilities"; Flags: uninsdeletevalue

; ============================================================
; .torrent file association
; ============================================================

Root: HKCU; Subkey: "Software\Classes\.torrent"; ValueType: string; ValueName: ""; ValueData: "NiTorrent.TorrentFile"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.torrent\OpenWithProgids"; ValueType: none; ValueName: "NiTorrent.TorrentFile"; Flags: uninsdeletevalue

Root: HKCU; Subkey: "Software\Classes\NiTorrent.TorrentFile"; ValueType: string; ValueName: ""; ValueData: "Torrent file"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\NiTorrent.TorrentFile\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\NiTorrent.TorrentFile\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Flags: uninsdeletekey

; ============================================================
; magnet: URL protocol association
; ============================================================

Root: HKCU; Subkey: "Software\Classes\magnet"; ValueType: string; ValueName: ""; ValueData: "URL:Magnet Protocol"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\magnet"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\magnet\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\magnet\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Flags: uninsdeletekey

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Запустить NiTorrent"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Удалить всё содержимое папки приложения.
; Это удалит и файлы, которые положил установщик, и файлы, которые приложение создало после установки.
Type: filesandordirs; Name: "{app}\*"

; После удаления содержимого удалить саму папку {app}, если она стала пустой.
Type: dirifempty; Name: "{app}"