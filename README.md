# 🐋 DeepSeek Harness 鲸鱼娘启动器（免安装版）

> Whale Girl Launcher for DeepSeek Harness Web

DeepSeek Harness Web 的 Windows 免安装启动器：**下载即用、无需管理员权限、不用手动配置 Node.js 环境与插件**。

它负责自动准备 DeepSeek Harness（`dsh`）运行环境、安装 `dsh-power-button` 启停插件与 `dshmarket` 插件市场、启动 DeepSeek Harness Web 服务，并让你选择本次使用的浏览器，以独立 Web 应用窗口打开——全程带着鲸鱼娘启动动画与品牌图标。

---

## ✨ 功能一览

| 功能 | 说明 |
| --- | --- |
| 🐋 鲸鱼娘启动动画 | 启动时展示动画与实时状态文字 |
| 🚀 一键安装启动 | 自动安装 / 查找 dsh 并启动 Web 服务 |
| 🔌 dsh-power-button | 右下角悬浮按钮：重启 / 仅停止 / 在线状态点 |
| 🧩 dshmarket 插件市场 | 自动安装，可在 Web 中浏览社区插件 |
| 🖥️ 浏览器选择 | 每次启动扫描 Edge / Chrome / Brave / Chromium 供你选择 |
| 🪟 独立 Web 窗口 | 居中窗口打开，使用独立浏览器配置目录 |
| 🔐 不动浏览器数据 | 不主动清除登录信息及其他浏览器配置 |
| 📝 日志记录 | 运行日志与错误日志写入用户目录，方便排查 |

---

## 🚀 快速开始

### 1. 获取启动器

- 直接使用 `DeepSeekHarness.exe`（推荐，最新构建）

无需安装，**双击即可运行**。

### 2. 双击运行

启动器会自动完成以下工作（首次运行需要联网）：

1. 检查 Node.js —— 未安装会弹出提示并打开 Node.js 官方下载页
2. 安装（或查找已装的）DeepSeek Harness（`@deepseek-ai/dsh`）
3. 准备 dsh 用户 profile 并安装依赖插件
4. 将 Web 页面标题改为「鲸鱼娘已就位，准备出发！」
5. 启动 dsh Web 服务（默认端口 3080）

### 3. 选择浏览器，开始使用

启动完成后会列出本机检测到的 Chromium 系浏览器，选择本次使用的浏览器，即可在独立 Web 窗口中打开 DeepSeek Harness Web。

---

## 🖥️ 系统要求

### 运行环境

- Windows 10 / Windows 11
- Node.js（含 npm）
- 至少一个可用的 Chromium 系浏览器：Microsoft Edge、Google Chrome、Brave 或 Chromium

> **关于 pnpm**：启动器优先使用已安装的 `pnpm`；如果没有，会通过已有的 `npm` 临时调用 `npx pnpm` 完成依赖安装。普通用户**不需要**单独安装 pnpm。

### 构建环境（仅开发者需要）

- .NET Framework 4.x 与 C# 编译器（`csc.exe`）
- Node.js

普通用户无需安装任何编译环境，直接运行 `DeepSeekHarness.exe` 即可。

---

## 📖 使用指南

### 启动界面

双击 exe 后出现鲸鱼娘启动界面：

- 顶部标题：鲸鱼娘启动器 / DeepSeek Harness · Web 服务
- 左侧：鲸鱼娘动画
- 中间：实时状态文字（如「正在启动 dsh Web 服务...」）
- 底部：进度条

![启动界面预览](startup-preview.png)

### 选择浏览器

每次启动都会重新扫描本机浏览器并弹出选择窗口（「鲸鱼娘要使用哪个浏览器？」）。选择后本次会话使用该浏览器，点击「使用此浏览器」确认。

### 独立 Web 窗口

- 以 `--app` 模式打开，看起来像一个独立应用窗口
- 窗口居中，默认约 1200×800（自动适配屏幕，最小 640×480）
- 使用独立的浏览器配置目录（见下文「数据与日志位置」），**不会**混用你日常浏览器的登录状态和配置

### 启停按钮（dsh-power-button）

启动器会自动安装启停插件，在 DeepSeek Harness Web 右下角显示悬浮按钮：

- **主按钮**：重启当前 dsh 服务并重新监听服务端口（`dsh-restart`）
- **辅助按钮**：仅停止当前 dsh 服务并释放端口（`dsh-stop`）
- **状态点**：显示服务在线、离线或重启中

插件来自独立仓库，首次运行通过 GitHub 安装：

https://github.com/huasheng33991/dsh-power-button

### 服务管理

除悬浮按钮外，服务的启动也可由启动器管理：

- 服务未运行时，双击 `DeepSeekHarness.exe` 即自动安装并启动服务
- 服务已在运行时，重新运行 exe 会直接打开独立窗口，不会重复安装或启动

### 插件市场（dshmarket）

自动安装最新稳定版 `dshmarket`（`latest`），可在 DeepSeek Harness Web 中浏览和安装社区插件。

---

## 📂 数据与日志位置

| 内容 | 位置 |
| --- | --- |
| dsh 用户 profile（插件、配置） | `%USERPROFILE%\.dsh\profiles\web` |
| 运行日志（标准输出） | `%USERPROFILE%\dsh-web.log` |
| 错误日志 | `%USERPROFILE%\dsh-web.err.log` |
| 独立浏览器配置目录 | `%LOCALAPPDATA%\DeepSeekHarness\BrowserProfile` |
| 服务端口 | 默认 `3080`（以 dsh 实际输出为准） |

> 说明：启动器**不会**清除或修改你日常浏览器的登录信息、配置和 Cookie；独立窗口使用单独的浏览器配置目录。

---

## ❓ 常见问题（FAQ）

### 提示「未检测到 Node.js」怎么办？

安装 Node.js 后重新运行即可。启动器会弹出提示并自动打开 Node.js 官方下载页：

https://nodejs.org/en/download

### 首次运行要下载什么？

- `@deepseek-ai/dsh`（DeepSeek Harness 本体）
- dsh profile 依赖（`dsh-power-button`、`dshmarket`、`dsh-base`、`dsh-web-app`）

均通过 npm 从网络安装，请保持网络畅通；如安装缓慢或失败，可考虑配置 npm 镜像源后重试。

### 为什么启动器优先用 pnpm？

`pnpm` 安装更快且对依赖处理更严格。启动器优先使用已安装的 `pnpm`；没有则临时通过 `npx pnpm` 调用，不需要你手动安装。

### 能同时开多个实例吗？

不建议。启动器会先检测服务端口上是否已有 dsh 在运行；如果已在运行，直接打开独立窗口，不会重复安装。

### 换一台电脑 / 换浏览器能用吗？

可以。启动器每次启动都会重新扫描浏览器；换电脑后首次运行会重新安装 dsh 与插件（profile 按用户隔离存放）。

### 会影响我日常使用的浏览器吗？

不会。独立窗口使用 `%LOCALAPPDATA%\DeepSeekHarness\BrowserProfile` 作为独立配置目录，不使用你日常的浏览器 profile。

### 需要管理员权限吗？

不需要。所有安装和配置都在用户目录内完成，无需管理员权限。

---

## 🛠️ 故障排查

启动器启动失败时会在界面弹出明确错误信息，同时将详细日志写入：

- `%USERPROFILE%\dsh-web.log` —— dsh 标准输出
- `%USERPROFILE%\dsh-web.err.log` —— dsh 错误输出

常见错误与处理：

| 错误信息 | 可能原因 | 处理 |
| --- | --- | --- |
| 未检测到 Node.js | Node.js 未安装或不在 PATH | 安装 Node.js 后重试 |
| DeepSeek dsh 安装失败 | 网络问题 / npm 源不可用 | 检查网络，或配置 npm 镜像后重试 |
| dsh 插件依赖安装失败（退出码 N） | 网络问题 / pnpm 不可用 | 检查网络与 npm 日志后重试 |
| dsh-power-button 安装后未找到 | 插件安装不完整 | 重新运行启动器 |
| dsh 进程已退出，退出码 N | dsh 运行时错误 | 查看 `dsh-web.err.log` |
| dsh Web 服务在 180 秒内未启动 | 启动缓慢 / 端口被占用 | 查看日志，稍后重试 |
| 未找到任何浏览器 | 系统中没有 Chromium 系浏览器 | 安装 Edge / Chrome / Brave 后重试 |

> 提示：查看日志可用记事本，或 PowerShell 命令
> `Get-Content "$env:USERPROFILE\dsh-web.err.log" -Tail 50`。

---

## 🧑‍💻 开发者指南

### 项目结构

```text
DeepSeekHarness-Whale-Girl-Installer-Launcher/
├── build/
│   └── Program.cs          # WinForms 启动器核心源码（全部功能）
├── DeepSeekHarness.exe     # 启动器主程序（免安装，双击运行）
├── DeepSeekHarness-Whale-Girl-Launcher-v2.0.0.zip  # 发布压缩包
├── deepseek-harness.ico    # 启动器图标（编译时嵌入）
├── download.gif            # 鲸鱼娘启动动画（编译时嵌入为资源）
├── startup-preview.png     # 启动界面预览图（文档用）
├── LICENSE                 # MIT 许可证
├── README.md               # 中文说明
└── README.en.md            # English documentation
```

### 构建

在项目根目录打开 PowerShell，使用 .NET Framework 自带的 C# 编译器：

```powershell
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' `
  /target:winexe `
  /platform:anycpu `
  /optimize+ `
  /nologo `
  /win32icon:deepseek-harness.ico `
  /r:System.dll `
  /r:System.Drawing.dll `
  /r:System.Windows.Forms.dll `
  /r:System.Web.Extensions.dll `
  /resource:download.gif,download.gif `
  /out:DeepSeekHarness.exe `
  build\Program.cs
```

### 发布打包

1. 构建生成 `DeepSeekHarness.exe`
2. 打包为 `DeepSeekHarness-Whale-Girl-Launcher-vX.Y.Z.zip`
3. 发布到 GitHub Releases（推荐）或直接分发 zip / exe

> 提示：`Program.cs` 中的 `AssemblyVersion` / `AssemblyFileVersion` 目前为 `2.0.0.0`，发布新版本时记得同步更新版本号。

### Roadmap

- [x] 鲸鱼娘启动动画
- [x] 独立 Web 窗口
- [x] 每次启动选择浏览器
- [x] dsh 启停插件
- [x] dshmarket 插件市场
- [x] 提供预编译 Windows Release（exe / zip）
- [ ] 自动更新启动器
- [ ] 更多 Chromium 浏览器支持
- [ ] 中英文界面切换

---

## 📜 许可证

本项目基于 [MIT License](LICENSE) 开源，© 2026 Yutou04-sa。

你可以自由使用、修改和分发本项目，只需保留版权声明。

---

## 🔗 项目地址

GitHub：

https://github.com/Yutou04-sa/DeepSeekHarness-Whale-Girl-Installer-Launcher

---

🐋 **鲸鱼娘已就位，准备出发！**
