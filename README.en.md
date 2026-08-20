# DeepSeekHarness Whale Girl Installer Launcher

[中文](README.md) | English

This is a pure C# installer and launcher for DeepSeek Harness Web on Windows. It prepares the dsh runtime, installs dsh-power-button and the dshmarket plugin marketplace, starts the Web service, and opens a centered standalone Web app window using an available Edge, Chrome, Brave, or Chromium installation.

The launcher includes the Whale Girl startup animation and branded icon. It also changes the Web page title to "Whale Girl is ready, let's set sail!". On first startup it scans local browsers and lets the user choose a default, then remembers that choice. After startup it opens the Web home page without showing the previous port warning window, and it does not clear browser sign-in or other profile settings.

## Included Source

- `build/Program.cs`: WinForms launcher source.
- [`dsh-power-button`](https://github.com/huasheng33991/dsh-power-button): installed from GitHub at startup, providing a floating restart button and a stop-only action.
- `dshmarket`: the visual plugin marketplace installed from npm at startup (`^1.15.0`).
- `download.gif`: Whale Girl startup animation.
- `deepseek-harness.ico`: launcher icon.

The compiled `DeepSeekHarness.exe`, dsh configuration, browser profiles, logs, and session data are intentionally excluded.

## Build Requirements

- Windows 10/11
- .NET Framework 4.x C# compiler
- Node.js
- npm (the launcher prefers pnpm and falls back to a temporary npx pnpm invocation)

Run this command from the repository root in PowerShell:

```powershell
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /target:winexe /platform:anycpu /optimize+ /nologo /win32icon:deepseek-harness.ico /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll /resource:download.gif,download.gif /out:DeepSeekHarness.exe build\Program.cs
```

Run `DeepSeekHarness.exe` after building. On first run, it installs the required dsh profile dependencies, dsh-power-button, and the plugin marketplace automatically.
