# DeepSeekHarness Whale Girl Installer Launcher

[中文](README.md) | English

This is a pure C# installer and launcher for DeepSeek Harness Web on Windows. It prepares the dsh runtime, installs the bundled port-control plugin, starts the Web service, and opens a centered standalone Web app window using Edge, Chrome, or Brave.

The launcher includes the Whale Girl startup animation and branded icon. It also changes the Web page title to "Whale Girl is ready, let's set sail!" and opens a fresh session by default without clearing browser sign-in or other profile settings.

## Included Source

- `build/Program.cs`: WinForms launcher source.
- `dsh-port-control/`: bundled port-control plugin providing `/dsh-stop`, `/dsh-restart`, and the fresh-session route.
- `download.gif`: Whale Girl startup animation.
- `deepseek-harness.ico`: launcher icon.

The compiled `DeepSeekHarness.exe`, dsh configuration, browser profiles, logs, and session data are intentionally excluded.

## Build Requirements

- Windows 10/11
- .NET Framework 4.x C# compiler
- Node.js
- pnpm (used by the launcher at runtime)

Run this command from the repository root in PowerShell:

```powershell
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /target:winexe /platform:anycpu /optimize+ /nologo /win32icon:deepseek-harness.ico /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll /resource:download.gif,download.gif /resource:dsh-port-control\package.json,dsh-port-control-package.json /resource:dsh-port-control\cordis.patch.yml,dsh-port-control-cordis.patch.yml /resource:dsh-port-control\lib\index.js,dsh-port-control-index.js /out:DeepSeekHarness.exe build\Program.cs
```

Run `DeepSeekHarness.exe` after building. On first run, it installs the required dsh profile dependencies and deploys the bundled port-control plugin automatically.
