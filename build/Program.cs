using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using System.Text.RegularExpressions;
using System.IO.Compression;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("DeepSeek Harness")]
[assembly: AssemblyDescription("DeepSeek Harness launcher")]
[assembly: AssemblyProduct("DeepSeek Harness")]
[assembly: AssemblyCompany("DeepSeek")]
[assembly: AssemblyVersion("3.0.0.0")]
[assembly: AssemblyFileVersion("3.0.0.0")]

internal static class Program
{
    private const int DefaultPort = 3080;
    private const string DshPackage = "@deepseek-ai/dsh";
    private const int MaxLogLines = 500;
    private static Process dshProcess;
    private static int servicePort = DefaultPort;
    private static readonly object logLock = new object();
    private static readonly string launcherLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dsh.log");

    private static void Log(string message)
    {
        try
        {
            lock (logLock)
            {
                File.AppendAllText(launcherLogPath, "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + message + Environment.NewLine, new UTF8Encoding(false));
                TrimLogIfNeeded();
            }
        }
        catch { }
    }

    private static void TrimLogIfNeeded()
    {
        try
        {
            string[] lines = File.ReadAllLines(launcherLogPath);
            if (lines.Length <= MaxLogLines) return;
            string[] tail = new string[MaxLogLines];
            Array.Copy(lines, lines.Length - MaxLogLines, tail, 0, MaxLogLines);
            File.WriteAllLines(launcherLogPath, tail, new UTF8Encoding(false));
        }
        catch { }
    }

    private static void ShowMessageBox(SplashForm splash, string message, MessageBoxIcon icon)
    {
        if (splash == null || splash.IsDisposed) return;
        if (splash.InvokeRequired)
        {
            try { splash.Invoke(new Action(delegate { MessageBox.Show(message, "DeepSeek Harness", MessageBoxButtons.OK, icon); })); }
            catch { }
        }
        else
        {
            MessageBox.Show(message, "DeepSeek Harness", MessageBoxButtons.OK, icon);
        }
    }

    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using (var splash = new SplashForm())
        {
            splash.Shown += delegate { ThreadPool.QueueUserWorkItem(delegate { Launch(splash); }); };
            Application.Run(splash);
        }
    }

    private static void Launch(SplashForm splash)
    {
        try
        {
            Log("========== DeepSeek Harness 启动器开始启动 ==========");
            Log("启动器日志文件: " + launcherLogPath);

            if (IsDshWebReady(servicePort))
            {
                Log("检测到 dsh Web 服务已在运行 (端口 " + servicePort + ")，直接打开独立窗口");
                splash.SetStatus("dsh 已在运行");
                splash.CloseWhenReady(OpenStandaloneWeb);
                return;
            }
            Log("dsh Web 服务未在运行 (端口 " + servicePort + ")，开始完整启动流程");

            string node = FindExecutable("node.exe", "C:\\Program Files\\nodejs\\node.exe", "C:\\Program Files (x86)\\nodejs\\node.exe");
            string npm = FindExecutable("npm.cmd", "C:\\Program Files\\nodejs\\npm.cmd", "C:\\Program Files (x86)\\nodejs\\npm.cmd");
            Log("检查 Node.js: node.exe = " + (node ?? "(未找到)"));
            Log("检查 npm: npm.cmd = " + (npm ?? "(未找到)"));
            if (node == null || npm == null)
            {
                Log("[警告] 未检测到 Node.js，提示用户安装并打开下载页");
                splash.SetStatus("需要安装 Node.js");
                ShowMessageBox(splash, "未检测到 Node.js，请先安装 Node.js。", MessageBoxIcon.Information);
                Process.Start(new ProcessStartInfo("https://nodejs.org/en/download") { UseShellExecute = true });
                splash.CloseWhenReady();
                return;
            }

            string dshBin = FindDshEntry(npm);
            Log("查找 dsh 入口: " + (dshBin ?? "(未找到)"));
            if (dshBin == null)
            {
                Log("未找到 dsh，执行: npm install --global " + DshPackage + " --no-fund --no-audit");
                splash.SetStatus("正在安装 DeepSeek dsh...");
                int dshExit = RunAndWait(npm, "install --global " + DshPackage + " --no-fund --no-audit");
                Log("dsh 安装命令退出码: " + dshExit);
                if (dshExit != 0)
                    throw new InvalidOperationException("DeepSeek dsh 安装失败。");
                dshBin = FindDshEntry(npm);
                Log("安装后重新查找 dsh 入口: " + (dshBin ?? "(未找到)"));
            }
            if (dshBin == null) throw new FileNotFoundException("安装完成后仍未找到 dsh 程序。");

            splash.SetStatus("正在安装启停按钮和插件市场...");
            string profile = EnsureProfile();
            PatchWebBranding(dshBin, profile);
            StartDsh(node, dshBin, profile);

            splash.SetStatus("正在启动 dsh Web 服务...");
            Log("等待 dsh Web 服务就绪 (最多 180 秒)...");
            for (int i = 0; i < 180; i++)
            {
                if (IsDshWebReady(servicePort))
                {
                    Log("Web 服务已就绪: http://127.0.0.1:" + servicePort + "/ (约 " + i + " 秒)");
                    splash.CloseWhenReady(OpenStandaloneWeb);
                    return;
                }
                if (dshProcess != null && dshProcess.HasExited)
                {
                    Log("[错误] dsh 进程已退出，退出码: " + dshProcess.ExitCode);
                    throw new InvalidOperationException("dsh 进程已退出，退出码：" + dshProcess.ExitCode + "。请检查应用目录中的 dsh.log。");
                }
                Thread.Sleep(1000);
            }
            Log("[错误] dsh Web 服务 180 秒内未启动");
            throw new TimeoutException("dsh Web 服务在 180 秒内未启动，请检查应用目录中的 dsh.log。");
        }
        catch (Exception error)
        {
            Log("[错误] 启动失败: " + error.GetType().Name + " - " + error.Message);
            Log("[错误] 异常堆栈: " + error.StackTrace);
            splash.SetStatus("启动失败");
            ShowMessageBox(splash, error.Message, MessageBoxIcon.Error);
            splash.CloseWhenReady();
        }
    }

    private static string EnsureProfile()
    {
        string profile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dsh", "profiles", "web");
        Directory.CreateDirectory(profile);
        Log("准备 profile 目录: " + profile);

        string powerButtonDir = Path.Combine(profile, "vendor", "dsh-power-button");
        if (!Directory.Exists(powerButtonDir))
        {
            Log("内置解压 dsh-power-button 插件到: " + powerButtonDir);
            ExtractEmbeddedZip("dsh-power-button.zip", powerButtonDir);
        }
        else
        {
            Log("dsh-power-button 插件已内置存在，跳过解压");
        }

        string packagePath = Path.Combine(profile, "package.json");
        var package = ReadPackage(packagePath);
        package["name"] = "dsh-profile-web";
        package["private"] = true;

        Dictionary<string, object> dependencies = GetDictionary(package, "dependencies");
        dependencies["dsh-power-button"] = "file:./vendor/dsh-power-button";
        dependencies["dshmarket"] = "latest";
        Log("声明依赖: dsh-power-button=file:./vendor/dsh-power-button (内置), dshmarket=latest");
        Dictionary<string, object> dsh = GetDictionary(package, "dsh");
        Dictionary<string, object> profileConfig = GetDictionary(dsh, "profile");
        List<object> bundles = GetList(profileConfig, "bundles");
        AddBundle(bundles, "@deepseek-ai/dsh-base");
        AddBundle(bundles, "@deepseek-ai/dsh-web-app");
        AddBundle(bundles, "dsh-power-button");
        AddBundle(bundles, "dshmarket");
        Log("声明 bundles: " + string.Join(", ", bundles.ConvertAll(b => b == null ? "" : b.ToString()).ToArray()));

        File.WriteAllText(packagePath, new JavaScriptSerializer().Serialize(package), new UTF8Encoding(false));
        Log("已写入 package.json");
        string cordisPath = Path.Combine(profile, "cordis.yml");
        if (!File.Exists(cordisPath)) { File.WriteAllText(cordisPath, "[]\r\n", new UTF8Encoding(false)); Log("已创建 cordis.yml (空)"); }
        string patchPath = Path.Combine(profile, "cordis.patch.yml");
        if (!File.Exists(patchPath)) { File.WriteAllText(patchPath, "[]\r\n", new UTF8Encoding(false)); Log("已创建 cordis.patch.yml (空)"); }
        string pnpm = FindExecutable("pnpm.cmd");
        string pnpmArguments = "install --config.minimum-release-age=0 --config.confirmModulesPurge=false --no-frozen-lockfile";
        Log("检查 pnpm: " + (pnpm ?? "(未找到)"));
        if (pnpm == null)
        {
            string npx = FindExecutable("npx.cmd");
            Log("pnpm 未找到，检查 npx: " + (npx ?? "(未找到)"));
            if (npx != null)
            {
                pnpm = npx;
                pnpmArguments = "--yes pnpm " + pnpmArguments;
                Log("将使用 npx pnpm 回退方案");
            }
        }
        if (pnpm == null) { Log("[错误] 未找到 pnpm 或 npx"); throw new InvalidOperationException("未找到 pnpm 或 npx，无法安装 dsh 插件依赖。"); }
        Log("执行安装: " + pnpm + " " + pnpmArguments + " (工作目录: " + profile + ")");
        int installExitCode = RunAndWait(pnpm, pnpmArguments, profile);
        Log("插件依赖安装退出码: " + installExitCode);
        if (installExitCode != 0)
        {
            Log("官方 npm registry 安装失败，尝试国内镜像 npmmirror...");
            installExitCode = RunAndWait(pnpm, pnpmArguments + " --registry=https://registry.npmmirror.com", profile);
            Log("国内镜像安装退出码: " + installExitCode);
        }
        if (installExitCode != 0)
            throw new InvalidOperationException("dsh 插件依赖安装失败，退出码：" + installExitCode + "。请检查网络连接和 npm 日志。");

        Log("检查插件安装结果 (目录: " + Path.Combine(profile, "node_modules") + "):");
        Log("  dsh-power-button: " + (Directory.Exists(Path.Combine(profile, "node_modules", "dsh-power-button")) ? "已安装" : "未找到"));
        Log("  dshmarket: " + (Directory.Exists(Path.Combine(profile, "node_modules", "dshmarket")) ? "已安装" : "未找到"));
        if (!Directory.Exists(Path.Combine(profile, "node_modules", "dsh-power-button")))
            throw new InvalidOperationException("dsh-power-button 安装后未找到，请重新启动安装器。");
        return profile;
    }

    private static void ExtractEmbeddedZip(string resourceName, string targetDir)
    {
        using (Stream source = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
        {
            if (source == null) throw new FileNotFoundException("Missing embedded resource: " + resourceName);
            Directory.CreateDirectory(targetDir);
            using (var zip = new ZipArchive(source, ZipArchiveMode.Read))
            {
                foreach (var entry in zip.Entries)
                {
                    string relativePath = entry.FullName.Replace('/', '\\');
                    if (relativePath.EndsWith("\\")) { Directory.CreateDirectory(Path.Combine(targetDir, relativePath)); continue; }
                    string destPath = Path.Combine(targetDir, relativePath);
                    string dir = Path.GetDirectoryName(destPath);
                    if (!String.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    using (Stream entryStream = entry.Open())
                    using (FileStream output = File.Create(destPath))
                        entryStream.CopyTo(output);
                }
            }
        }
    }

    private static void PatchWebBranding(string dshBin, string profile)
    {
        const string title = "鲸鱼娘已就位，准备出发！";
        var candidates = new List<string>();
        string profileFrontend = Path.Combine(profile, "node_modules", "@deepseek-ai", "dsh-web-frontend", "dist");
        candidates.Add(profileFrontend);

        string dshRoot = Path.GetDirectoryName(Path.GetDirectoryName(dshBin));
        if (!String.IsNullOrEmpty(dshRoot))
        {
            string scopedRoot = Path.GetDirectoryName(dshRoot);
            string modulesRoot = scopedRoot == null ? null : Path.GetDirectoryName(scopedRoot);
            if (!String.IsNullOrEmpty(modulesRoot))
                candidates.Add(Path.Combine(modulesRoot, "@deepseek-ai", "dsh-web-frontend", "dist"));
        }

        foreach (string dist in candidates)
        {
            if (String.IsNullOrEmpty(dist)) continue;
            PatchText(Path.Combine(dist, "index.html"), title, "<title>", "</title>");
            PatchText(Path.Combine(dist, "manifest.webmanifest"), title, "\"name\": \"", "\"");
        }
    }

    private static void PatchText(string path, string replacement, string prefix, string suffix)
    {
        if (!File.Exists(path)) return;
        try
        {
            string text = File.ReadAllText(path, Encoding.UTF8);
            int start = text.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            if (start < 0) return;
            start += prefix.Length;
            int end = text.IndexOf(suffix, start, StringComparison.OrdinalIgnoreCase);
            if (end < 0) return;
            string next = text.Substring(0, start) + replacement + text.Substring(end);
            if (!String.Equals(text, next, StringComparison.Ordinal)) File.WriteAllText(path, next, new UTF8Encoding(false));
        }
        catch { }
    }

    private static Dictionary<string, object> ReadPackage(string path)
    {
        if (File.Exists(path))
        {
            try
            {
                object parsed = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(path));
                var dictionary = parsed as Dictionary<string, object>;
                if (dictionary != null) return dictionary;
            }
            catch { }
        }
        return new Dictionary<string, object>();
    }

    private static Dictionary<string, object> GetDictionary(Dictionary<string, object> parent, string key)
    {
        object value;
        Dictionary<string, object> dictionary;
        if (parent.TryGetValue(key, out value) && (dictionary = value as Dictionary<string, object>) != null) return dictionary;
        dictionary = new Dictionary<string, object>();
        parent[key] = dictionary;
        return dictionary;
    }

    private static List<object> GetList(Dictionary<string, object> parent, string key)
    {
        object value;
        List<object> result;
        if (parent.TryGetValue(key, out value))
        {
            var list = value as ArrayList;
            if (list != null)
            {
                result = new List<object>(list.ToArray());
                parent[key] = result;
                return result;
            }
            var array = value as object[];
            if (array != null)
            {
                result = new List<object>(array);
                parent[key] = result;
                return result;
            }
        }
        result = new List<object>();
        parent[key] = result;
        return result;
    }

    private static void AddBundle(List<object> bundles, string name)
    {
        foreach (object value in bundles) if (String.Equals(value as string, name, StringComparison.Ordinal)) return;
        bundles.Add(name);
    }

    private static void StartDsh(string node, string dshBin, string profile)
    {
        string log = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dsh.log");
        string errorLog = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dsh.log");
        Log("启动 dsh 进程: " + node + " " + Quote(dshBin) + " web");
        Log("  工作目录: " + profile);
        Log("  dsh 输出统一写入: " + log);
        var start = new ProcessStartInfo
        {
            FileName = node,
            Arguments = Quote(dshBin) + " web",
            WorkingDirectory = profile,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        dshProcess = Process.Start(start);
        Log("dsh 进程已启动 (PID: " + dshProcess.Id + ")");
        dshProcess.OutputDataReceived += delegate(object sender, DataReceivedEventArgs args) { HandleOutput(log, args.Data); };
        dshProcess.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs args) { AppendLog(errorLog, args.Data); };
        dshProcess.BeginOutputReadLine();
        dshProcess.BeginErrorReadLine();
    }

    private static void AppendLog(string path, string line)
    {
        if (String.IsNullOrEmpty(line)) return;
        try
        {
            lock (logLock)
            {
                File.AppendAllText(path, line + Environment.NewLine, new UTF8Encoding(false));
                TrimLogIfNeeded();
            }
        }
        catch { }
    }

    private static void HandleOutput(string path, string line)
    {
        AppendLog(path, line);
        if (String.IsNullOrEmpty(line)) return;
        Match match = Regex.Match(line, @"https?://(?:127\.0\.0\.1|localhost):(\d+)", RegexOptions.IgnoreCase);
        int parsed;
        if (match.Success && Int32.TryParse(match.Groups[1].Value, out parsed) && parsed > 0 && parsed <= 65535)
            servicePort = parsed;
    }

    private static string FindDshEntry(string npm)
    {
        string globalRoot = RunAndRead(npm, "root -g");
        if (!String.IsNullOrWhiteSpace(globalRoot))
        {
            string globalEntry = Path.Combine(globalRoot.Trim(), "@deepseek-ai", "dsh", "lib", "bin.js");
            if (File.Exists(globalEntry)) return globalEntry;
        }

        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string[] known = {
            Path.Combine(appData, "npm", "node_modules", "@deepseek-ai", "dsh", "lib", "bin.js"),
            "C:\\Program Files\\nodejs\\node_modules\\@deepseek-ai\\dsh\\lib\\bin.js",
            "C:\\Program Files (x86)\\nodejs\\node_modules\\@deepseek-ai\\dsh\\lib\\bin.js"
        };
        foreach (string candidate in known) if (File.Exists(candidate)) return candidate;

        string cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "npm-cache", "_npx");
        if (!Directory.Exists(cache)) return null;
        string newest = null;
        DateTime newestTime = DateTime.MinValue;
        foreach (string file in FindFiles(cache, "bin.js"))
        {
            if (!file.EndsWith("\\node_modules\\@deepseek-ai\\dsh\\lib\\bin.js", StringComparison.OrdinalIgnoreCase)) continue;
            DateTime modified = File.GetLastWriteTimeUtc(file);
            if (modified > newestTime) { newestTime = modified; newest = file; }
        }
        return newest;
    }

    private static IEnumerable<string> FindFiles(string root, string name)
    {
        var folders = new Stack<string>();
        folders.Push(root);
        while (folders.Count > 0)
        {
            string folder = folders.Pop();
            string[] files = null;
            string[] children = null;
            try { files = Directory.GetFiles(folder, name); children = Directory.GetDirectories(folder); } catch { }
            if (files != null) foreach (string file in files) yield return file;
            if (children != null) foreach (string child in children) folders.Push(child);
        }
    }

    private static string FindExecutable(string fileName, params string[] knownPaths)
    {
        foreach (string path in knownPaths) if (File.Exists(path)) return path;
        string pathVariable = Environment.GetEnvironmentVariable("PATH") ?? String.Empty;
        foreach (string folder in pathVariable.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                string candidate = Path.Combine(folder.Trim().Trim('"'), fileName);
                if (File.Exists(candidate)) return candidate;
            }
            catch { }
        }
        return null;
    }

    private static ProcessStartInfo BuildStartInfo(string fileName, string arguments, string workingDirectory, bool redirectOutput)
    {
        ProcessStartInfo start;
        if (fileName.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) || fileName.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
        {
            string commandLine = fileName + (String.IsNullOrEmpty(arguments) ? "" : " " + arguments);
            start = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c " + Quote(commandLine),
                WorkingDirectory = workingDirectory ?? String.Empty,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }
        else
        {
            start = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory ?? String.Empty,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }
        if (redirectOutput) start.RedirectStandardOutput = true;
        return start;
    }

    private static int RunAndWait(string fileName, string arguments, string workingDirectory = null)
    {
        using (Process process = Process.Start(BuildStartInfo(fileName, arguments, workingDirectory, false)))
        {
            if (!process.WaitForExit(600000))
            {
                try { process.Kill(); } catch { }
                return -1;
            }
            return process.ExitCode;
        }
    }

    private static string RunAndRead(string fileName, string arguments)
    {
        using (Process process = Process.Start(BuildStartInfo(fileName, arguments, null, true)))
        {
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? output : String.Empty;
        }
    }

    private static bool IsDshWebReady(int port)
    {
        try
        {
            var request = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:" + port + "/");
            request.Method = "GET";
            request.Timeout = 1000;
            request.ReadWriteTimeout = 1000;
            using (var response = (HttpWebResponse)request.GetResponse())
                return ((int)response.StatusCode) < 500;
        }
        catch (WebException error)
        {
            var response = error.Response as HttpWebResponse;
            if (response == null) return false;
            using (response) return ((int)response.StatusCode) < 500;
        }
        catch { return false; }
    }

    private static void OpenStandaloneWeb()
    {
        string url = "http://127.0.0.1:" + servicePort + "/";
        List<BrowserOption> browsers = DetectBrowsers();
        string browserNames = "";
        foreach (BrowserOption b in browsers) browserNames = (browserNames.Length == 0 ? "" : browserNames + ", ") + b.Name;
        Log("检测到浏览器 (" + browsers.Count + " 个): " + (browserNames.Length == 0 ? "(无)" : browserNames));
        string browser = ChooseBrowser(browsers);
        if (browser == null)
        {
            Log("未检测到 Chromium 浏览器，使用系统默认浏览器打开: " + url);
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            return;
        }
        Log("使用浏览器: " + browser);

        string profile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeepSeekHarness", "BrowserProfile");
        Directory.CreateDirectory(profile);
        Rectangle workArea = Screen.PrimaryScreen.WorkingArea;
        int width = Math.Min(1200, Math.Max(640, workArea.Width - 40));
        int height = Math.Min(800, Math.Max(480, workArea.Height - 40));
        int left = workArea.Left + (workArea.Width - width) / 2;
        int top = workArea.Top + (workArea.Height - height) / 2;
        string args = "--app=" + Quote(url) +
                      " --user-data-dir=" + Quote(profile) +
                      " --disable-sync --no-sync --no-first-run --no-default-browser-check" +
                      " --window-size=" + width + "," + height +
                      " --window-position=" + left + "," + top;
        Process.Start(new ProcessStartInfo { FileName = browser, Arguments = args, UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(browser) });
    }

    private static List<BrowserOption> DetectBrowsers()
    {
        var result = new List<BrowserOption>();
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        AddBrowser(result, "Microsoft Edge", Path.Combine(programFiles, "Microsoft", "Edge", "Application", "msedge.exe"));
        AddBrowser(result, "Microsoft Edge", Path.Combine(programFilesX86, "Microsoft", "Edge", "Application", "msedge.exe"));
        AddBrowser(result, "Microsoft Edge", Path.Combine(localAppData, "Microsoft", "Edge", "Application", "msedge.exe"));
        AddBrowser(result, "Google Chrome", Path.Combine(programFiles, "Google", "Chrome", "Application", "chrome.exe"));
        AddBrowser(result, "Google Chrome", Path.Combine(programFilesX86, "Google", "Chrome", "Application", "chrome.exe"));
        AddBrowser(result, "Google Chrome", Path.Combine(localAppData, "Google", "Chrome", "Application", "chrome.exe"));
        AddBrowser(result, "Brave", Path.Combine(programFiles, "BraveSoftware", "Brave-Browser", "Application", "brave.exe"));
        AddBrowser(result, "Brave", Path.Combine(programFilesX86, "BraveSoftware", "Brave-Browser", "Application", "brave.exe"));
        AddBrowser(result, "Brave", Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "Application", "brave.exe"));
        AddBrowser(result, "Chromium", FindExecutable("chromium.exe"));
        AddBrowser(result, "Chromium", FindExecutable("chrome.exe"));
        return result;
    }

    private static void AddBrowser(List<BrowserOption> browsers, string name, string path)
    {
        if (String.IsNullOrEmpty(path) || !File.Exists(path)) return;
        foreach (BrowserOption existing in browsers)
            if (String.Equals(existing.Path, path, StringComparison.OrdinalIgnoreCase)) return;
        browsers.Add(new BrowserOption { Name = name, Path = path });
    }

    private static string ChooseBrowser(List<BrowserOption> browsers)
    {
        if (browsers.Count == 0) return null;
        using (var chooser = new BrowserChoiceForm(browsers))
        {
            if (chooser.ShowDialog() == DialogResult.OK && chooser.SelectedPath != null)
                return chooser.SelectedPath;
        }
        return browsers[0].Path;
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }
}

internal sealed class BrowserOption
{
    public string Name;
    public string Path;
}

internal sealed class BrowserChoiceForm : Form
{
    public string SelectedPath { get; private set; }

    public BrowserChoiceForm(List<BrowserOption> browsers)
    {
        Text = "鲸鱼娘 · 选择 Web 浏览器";
        ClientSize = new Size(520, 360);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(247, 250, 252);
        TopMost = true;

        var header = new Panel { Dock = DockStyle.Top, Height = 82, BackColor = Color.FromArgb(32, 112, 184) };
        header.Controls.Add(new Label { AutoSize = true, Left = 24, Top = 14, ForeColor = Color.White, Font = new Font("Microsoft YaHei", 17, FontStyle.Bold), Text = "鲸鱼娘要使用哪个浏览器？" });
        header.Controls.Add(new Label { AutoSize = true, Left = 26, Top = 48, ForeColor = Color.FromArgb(218, 238, 255), Font = new Font("Microsoft YaHei", 9), Text = "首次启动请选择独立 Web 页面使用的浏览器" });

        var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 16, 24, 16) };
        var choices = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = Color.Transparent };
        foreach (BrowserOption browser in browsers)
        {
            var radio = new RadioButton
            {
                AutoSize = false,
                Width = 450,
                Height = 42,
                Text = browser.Name + "\r\n" + browser.Path,
                Tag = browser.Path,
                Font = new Font("Microsoft YaHei", 10),
                ForeColor = Color.FromArgb(38, 50, 56),
                Padding = new Padding(4, 2, 0, 2)
            };
            radio.CheckedChanged += delegate(object sender, EventArgs args)
            {
                var selected = sender as RadioButton;
                if (selected != null && selected.Checked) SelectedPath = selected.Tag as string;
            };
            choices.Controls.Add(radio);
        }
        if (choices.Controls.Count > 0) ((RadioButton)choices.Controls[0]).Checked = true;

        var use = new Button { Text = "使用此浏览器", Width = 120, Height = 32, Dock = DockStyle.Bottom, DialogResult = DialogResult.OK, Font = new Font("Microsoft YaHei", 9) };
        AcceptButton = use;
        body.Controls.Add(choices);
        body.Controls.Add(use);
        Controls.Add(body);
        Controls.Add(header);
    }

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private System.Windows.Forms.Timer topmostTimer;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x00000008; // WS_EX_TOPMOST
            return cp;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
        Activate();
        topmostTimer = new System.Windows.Forms.Timer { Interval = 250 };
        topmostTimer.Tick += delegate
        {
            if (IsDisposed) { try { topmostTimer.Stop(); topmostTimer.Dispose(); } catch { } return; }
            SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
        };
        topmostTimer.Start();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (topmostTimer != null) { try { topmostTimer.Stop(); topmostTimer.Dispose(); } catch { } }
        base.OnFormClosed(e);
    }
}

internal sealed class SplashForm : Form
{
    private readonly Label status;
    private readonly MemoryStream imageStream;

    public SplashForm()
    {
        Text = "鲸鱼娘启动器";
        ClientSize = new Size(440, 220);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(247, 250, 252);
        TopMost = true;

        var header = new Panel { Dock = DockStyle.Top, Height = 76, BackColor = Color.FromArgb(32, 112, 184) };
        header.Controls.Add(new Label { AutoSize = true, Left = 24, Top = 13, ForeColor = Color.White, Font = new Font("Microsoft YaHei", 18, FontStyle.Bold), Text = "鲸鱼娘启动器" });
        header.Controls.Add(new Label { AutoSize = true, Left = 26, Top = 47, ForeColor = Color.FromArgb(218, 238, 255), Font = new Font("Microsoft YaHei", 9), Text = "DeepSeek Harness · Web 服务" });

        var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18, 16, 24, 18) };
        var animation = new PictureBox { Dock = DockStyle.Left, Width = 120, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent };
        imageStream = new MemoryStream();
        using (Stream resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("download.gif"))
        {
            if (resource == null) throw new FileNotFoundException("Missing embedded resource: download.gif");
            resource.CopyTo(imageStream);
        }
        imageStream.Position = 0;
        animation.Image = Image.FromStream(imageStream);
        status = new Label { Dock = DockStyle.Top, Height = 40, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(38, 50, 56), Font = new Font("Microsoft YaHei", 11), Text = "正在准备启动..." };
        var hint = new Label { Dock = DockStyle.Bottom, Height = 24, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(104, 120, 130), Font = new Font("Microsoft YaHei", 9), Text = "鲸鱼娘正在为你准备 Web 服务" };
        var progress = new ProgressBar { Dock = DockStyle.Bottom, Height = 8, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 28 };
        body.Controls.Add(hint);
        body.Controls.Add(progress);
        body.Controls.Add(status);
        body.Controls.Add(animation);
        Controls.Add(body);
        Controls.Add(header);
    }

    public void SetStatus(string value)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(new Action<string>(SetStatus), value); return; }
        status.Text = value;
    }

    public void CloseWhenReady()
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(new Action(CloseWhenReady)); return; }
        Close();
    }

    public void CloseWhenReady(Action afterClosed)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(new Action<Action>(CloseWhenReady), afterClosed); return; }
        var thread = new Thread(new ThreadStart(delegate
        {
            Thread.Sleep(180);
            afterClosed();
        }));
        thread.IsBackground = false;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Close();
    }

    protected override void WndProc(ref Message m)
    {
        const int WM_NCHITTEST = 0x84;
        const int HTCAPTION = 0x2;
        if (m.Msg == WM_NCHITTEST)
        {
            m.Result = (IntPtr)HTCAPTION;
            return;
        }
        base.WndProc(ref m);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) imageStream.Dispose();
        base.Dispose(disposing);
    }

}
