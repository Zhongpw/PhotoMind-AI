; PhotoMind AI - Inno Setup 安装脚本
; 生成自包含 x64 安装程序。按用户安装（无需管理员权限）。
;
; 用法:
;   生产构建:  ISCC.exe PhotoMindAI.iss
;   测试构建(无图标/无注册表，用于受限环境安装测试):  ISCC.exe /DTESTBUILD PhotoMindAI.iss

#define AppName "PhotoMind AI"
#define AppVersion "1.0.0"
#define AppPublisher "PhotoMind AI"
#define AppExeName "PhotoMind.App.exe"
#define AppSource "..\src\PhotoMind.App\bin\Release\net8.0-windows\win-x64\publish"

[Setup]
AppId={{B7E9C4A2-4F1D-4C2B-9A6E-3D8B5E7A1F00}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
VersionInfoVersion={#AppVersion}
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
DefaultDirName={localappdata}\Programs\PhotoMind AI
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=output
#ifdef TESTBUILD
OutputBaseFilename=PhotoMindAI-Setup-Test
CreateUninstallRegKey=no
#else
OutputBaseFilename=PhotoMindAI-Setup-{#AppVersion}
CreateUninstallRegKey=yes
#endif
SetupIconFile=..\src\PhotoMind.App\Assets\app.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
MinVersion=10.0.19045
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#AppSource}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

#ifndef TESTBUILD
[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon
#endif

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[Code]
{ Uninstaller 不删除用户数据（默认行为：只删程序文件，%LOCALAPPDATA%\PhotoMind AI 保留）。 }
