# DeepSeekHarness 鲸鱼娘安装启动器

中文 | [English](README.en.md)

这是一个面向 Windows 的纯 C# DeepSeek Harness Web 安装启动器。它会自动准备 dsh 运行环境、安装 dsh-power-button 启停插件和 dshmarket 插件市场、启动 Web 服务，并使用 Edge、Chrome 或 Brave 打开居中的独立 Web 应用窗口。

启动器保留鲸鱼娘启动动画和品牌图标，并会将 Web 页面标题设置为“鲸鱼娘已就位，准备出发！”。Web 服务启动后直接打开首页，不再显示端口警告窗口，也不会清除登录信息和其他浏览器配置。

### 核心源码

- `build/Program.cs`：WinForms 启动器源码。
- [`dsh-power-button`](https://github.com/huasheng33991/dsh-power-button)：启动时从 GitHub 自动安装的启停插件，提供悬浮重启按钮和仅停止按钮。
- `dshmarket`：启动时从 npm 自动安装的可视化插件市场（`^1.15.0`）。
- `download.gif`：鲸鱼娘启动动画。
- `deepseek-harness.ico`：启动器图标。

仓库不会提交编译后的 `DeepSeekHarness.exe`、dsh 配置、浏览器配置、日志或会话数据。

### 构建要求

- Windows 10/11
- .NET Framework 4.x C# 编译器
- Node.js
- npm（启动器会优先使用 pnpm；未安装 pnpm 时自动通过 npx 临时调用）

在仓库根目录打开 PowerShell，执行下面的构建命令：

```powershell
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /target:winexe /platform:anycpu /optimize+ /nologo /win32icon:deepseek-harness.ico /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll /resource:download.gif,download.gif /out:DeepSeekHarness.exe build\Program.cs
```

构建完成后运行 `DeepSeekHarness.exe`。首次启动会自动安装所需的 dsh 配置依赖、dsh-power-button 和插件市场。
