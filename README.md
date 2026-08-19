# DeepSeekHarness鲸鱼娘安装启动器

Windows-only C# launcher for DeepSeek Harness Web. It starts dsh, opens a centered standalone Web window, installs the bundled port-control plugin, and applies the Whale Girl title branding.

## Included source

- `build/Program.cs`: WinForms launcher source.
- `dsh-port-control/`: embedded dsh plugin source for `/dsh-stop`, `/dsh-restart`, and the fresh-session route.
- `download.gif`: Whale Girl startup animation.
- `deepseek-harness.ico`: launcher icon.

The compiled `DeepSeekHarness.exe`, dsh profile data, browser profile, and session data are intentionally excluded.

## Build

Prerequisites: Windows, .NET Framework 4.x compiler, Node.js, and pnpm available to the launcher at runtime.

Run this command from the repository root in PowerShell:

```powershell
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /target:winexe /platform:anycpu /optimize+ /nologo /win32icon:deepseek-harness.ico /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll /resource:download.gif,download.gif /resource:dsh-port-control\package.json,dsh-port-control-package.json /resource:dsh-port-control\cordis.patch.yml,dsh-port-control-cordis.patch.yml /resource:dsh-port-control\lib\index.js,dsh-port-control-index.js /out:DeepSeekHarness.exe build\Program.cs
```

Run `DeepSeekHarness.exe` after building. On its first run, it installs the required dsh profile dependencies and deploys the embedded port-control plugin automatically.
