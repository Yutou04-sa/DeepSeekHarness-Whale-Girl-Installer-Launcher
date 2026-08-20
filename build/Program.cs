using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using System.Text.RegularExpressions;

[assembly: AssemblyTitle("DeepSeek Harness")]
[assembly: AssemblyDescription("DeepSeek Harness launcher")]
[assembly: AssemblyProduct("DeepSeek Harness")]
[assembly: AssemblyCompany("DeepSeek")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

internal static class Program
{
    private const int DefaultPort = 3080;
    private const string DshPackage = "@deepseek-ai/dsh";
    private static Process dshProcess;
    private static int servicePort = DefaultPort;

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
            if (IsPortListening(servicePort))
            {
                splash.SetStatus("dsh 已在运行");
                splash.CloseWhenReady(OpenStandaloneWeb);
                return;
            }

            string node = FindExecutable("node.exe", "C:\\Program Files\\nodejs\\node.exe", "C:\\Program Files (x86)\\nodejs\\node.exe");
            string npm = FindExecutable("npm.cmd", "C:\\Program Files\\nodejs\\npm.cmd", "C:\\Program Files (x86)\\nodejs\\npm.cmd");
            if (node == null || npm == null)
            {
                splash.SetStatus("需要安装 Node.js");
                MessageBox.Show("未检测到 Node.js，请先安装 Node.js。", "DeepSeek Harness", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Process.Start(new ProcessStartInfo("https://nodejs.org/en/download") { UseShellExecute = true });
                splash.CloseWhenReady();
                return;
            }

            string dshBin = FindDshEntry(npm);
            if (dshBin == null)
            {
                splash.SetStatus("正在安装 DeepSeek dsh...");
                if (RunAndWait(npm, "install --global " + DshPackage + " --no-fund --no-audit") != 0)
                    throw new InvalidOperationException("DeepSeek dsh 安装失败。");
                dshBin = FindDshEntry(npm);
            }
            if (dshBin == null) throw new FileNotFoundException("安装完成后仍未找到 dsh 程序。");

            splash.SetStatus("正在安装启停按钮和插件市场...");
            string profile = EnsureProfile();
            PatchWebBranding(dshBin, profile);
            StartDsh(node, dshBin, profile);

            splash.SetStatus("正在启动 dsh Web 服务...");
            for (int i = 0; i < 180; i++)
            {
                if (IsPortListening(servicePort))
                {
                    splash.CloseWhenReady(OpenStandaloneWeb);
                    return;
                }
                Thread.Sleep(1000);
            }
            throw new TimeoutException("dsh Web 服务在 180 秒内未启动，请检查用户目录中的 dsh-web.err.log。");
        }
        catch (Exception error)
        {
            splash.SetStatus("启动失败");
            MessageBox.Show(error.Message, "DeepSeek Harness", MessageBoxButtons.OK, MessageBoxIcon.Error);
            splash.CloseWhenReady();
        }
    }

    private static string EnsureProfile()
    {
        string profile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dsh", "profiles", "web");
        Directory.CreateDirectory(profile);

        string packagePath = Path.Combine(profile, "package.json");
        var package = ReadPackage(packagePath);
        package["name"] = "dsh-profile-web";
        package["private"] = true;

        Dictionary<string, object> dependencies = GetDictionary(package, "dependencies");
        dependencies["dsh-power-button"] = "github:huasheng33991/dsh-power-button";
        dependencies["dshmarket"] = "^1.15.0";
        Dictionary<string, object> dsh = GetDictionary(package, "dsh");
        Dictionary<string, object> profileConfig = GetDictionary(dsh, "profile");
        List<object> bundles = GetList(profileConfig, "bundles");
        AddBundle(bundles, "@deepseek-ai/dsh-base");
        AddBundle(bundles, "@deepseek-ai/dsh-web-app");
        AddBundle(bundles, "dsh-power-button");
        AddBundle(bundles, "dshmarket");

        File.WriteAllText(packagePath, new JavaScriptSerializer().Serialize(package), new UTF8Encoding(false));
        string cordisPath = Path.Combine(profile, "cordis.yml");
        if (!File.Exists(cordisPath)) File.WriteAllText(cordisPath, "[]\r\n", new UTF8Encoding(false));
        string patchPath = Path.Combine(profile, "cordis.patch.yml");
        if (!File.Exists(patchPath)) File.WriteAllText(patchPath, "[]\r\n", new UTF8Encoding(false));
        string pnpm = FindExecutable("pnpm.cmd");
        string pnpmArguments = "install --config.minimum-release-age=0 --config.confirmModulesPurge=false --no-frozen-lockfile";
        if (pnpm == null)
        {
            string npx = FindExecutable("npx.cmd");
            if (npx != null)
            {
                pnpm = npx;
                pnpmArguments = "--yes pnpm " + pnpmArguments;
            }
        }
        if (pnpm != null) RunAndWait(pnpm, pnpmArguments, profile);
        return profile;
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

    private static void WriteResource(string resourceName, string target)
    {
        using (Stream source = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
        {
            if (source == null) throw new FileNotFoundException("Missing embedded resource: " + resourceName);
            using (FileStream output = File.Create(target)) source.CopyTo(output);
        }
    }

    private static void StartDsh(string node, string dshBin, string profile)
    {
        string log = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "dsh-web.log");
        string errorLog = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "dsh-web.err.log");
        var start = new ProcessStartInfo
        {
            FileName = node,
            Arguments = Quote(dshBin) + " web",
            WorkingDirectory = profile,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        dshProcess = Process.Start(start);
        dshProcess.OutputDataReceived += delegate(object sender, DataReceivedEventArgs args) { HandleOutput(log, args.Data); };
        dshProcess.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs args) { AppendLog(errorLog, args.Data); };
        dshProcess.BeginOutputReadLine();
        dshProcess.BeginErrorReadLine();
    }

    private static void AppendLog(string path, string line)
    {
        if (!String.IsNullOrEmpty(line)) File.AppendAllText(path, line + Environment.NewLine, new UTF8Encoding(false));
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

    private static int RunAndWait(string fileName, string arguments, string workingDirectory = null)
    {
        using (Process process = Process.Start(new ProcessStartInfo { FileName = fileName, Arguments = arguments, WorkingDirectory = workingDirectory ?? String.Empty, UseShellExecute = false, CreateNoWindow = true }))
        {
            process.WaitForExit();
            return process.ExitCode;
        }
    }

    private static string RunAndRead(string fileName, string arguments)
    {
        using (Process process = Process.Start(new ProcessStartInfo { FileName = fileName, Arguments = arguments, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true }))
        {
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? output : String.Empty;
        }
    }

    private static bool IsPortListening(int port)
    {
        using (var client = new TcpClient())
        {
            try
            {
                IAsyncResult result = client.BeginConnect("127.0.0.1", port, null, null);
                return result.AsyncWaitHandle.WaitOne(100);
            }
            catch { return false; }
        }
    }

    private static void OpenStandaloneWeb()
    {
        string url = "http://127.0.0.1:" + servicePort + "/";
        string[] candidates = {
            "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe",
            "C:\\Program Files\\Microsoft\\Edge\\Application\\msedge.exe",
            "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe",
            "C:\\Program Files (x86)\\Google\\Chrome\\Application\\chrome.exe",
            "C:\\Program Files\\BraveSoftware\\Brave-Browser\\Application\\brave.exe"
        };
        string browser = null;
        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate)) { browser = candidate; break; }
        }
        if (browser == null)
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            return;
        }

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

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
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

    protected override void Dispose(bool disposing)
    {
        if (disposing) imageStream.Dispose();
        base.Dispose(disposing);
    }

}
