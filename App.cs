using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace ForTheEmperor;

public sealed class App : Application
{
    internal static App CurrentApp => (App)Current;
    internal Preferences Settings = new();
    internal PetWindow Pet = null!;
    internal CommandWindow? Panel;
    internal bool Preview;
    private Forms.NotifyIcon? tray;
    private readonly CancellationTokenSource stop = new();
    private readonly string owner = Guid.NewGuid().ToString("N");
    private bool registered;
    private bool watching;
    private Mutex? mutex;
    private string menuStatus = "尚未连接";
    internal string MenuStatus => L.T(menuStatus);

    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            var saved = Preferences.Load();
            string? languageOverride = null;
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "--language" && L.Supported(args[i + 1])) languageOverride = args[i + 1];
            L.SetLanguage(languageOverride ?? saved.Language ?? (System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh" ? "zh-CN" : "en"));
            if (args.Length == 4 && args[0] == "--watch") { ShellMenu.Watch(int.Parse(args[1]), long.Parse(args[2]), args[3]); return 0; }
            if (args.Length > 0 && args[0] == "--unregister") { ShellMenu.Remove(); return 0; }
            if (args.Length > 0 && args[0] == "--self-test") return Checks.Run(args.Length > 1 ? args[1] : "artifacts");
            if (args.Length > 0 && args[0] == "--render-preview") return Checks.Render(args.Length > 1 ? args[1] : "artifacts");
            if (args.Length > 0 && args[0] == "--render-animation") return Checks.RenderAnimation(args.Length > 1 ? args[1] : "artifacts");
            if (args.Length == 3 && args[0] == "--probe-icon") return Checks.ProbeDesktop(args[1], args[2] == "present");
            if (args.Length == 2 && args[0] == "--execute")
            {
                var cursor = Native.Cursor();
                var locate = Task.Run(() => Native.LocateDesktopIcon(args[1], cursor));
                var point = locate.Wait(900) ? locate.Result : cursor;
                string result;
                try { result = Bridge.Send(new("execute", args[1], point.X, point.Y)).GetAwaiter().GetResult(); }
                catch { result = L.T("请先启动桌面战士，再从文件右键菜单执行任务。"); }
                if (result != "OK") MessageBox.Show(result, L.T("以帝皇之名"), MessageBoxButton.OK, MessageBoxImage.Information);
                return result == "OK" ? 0 : 1;
            }
            var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown, Preview = Array.Exists(args, a => a is "--preview" or "--smoke-test") };
            app.mutex = new Mutex(true, @"Local\" + Bridge.Id, out bool first);
            if (!first)
            {
                try { Bridge.Send(new("show")).GetAwaiter().GetResult(); } catch { }
                app.mutex.Dispose(); return 0;
            }
            app.Settings = app.Preview ? new() : Preferences.Load();
            if (languageOverride != null) app.Settings.Language = languageOverride;
            app.DispatcherUnhandledException += (_, e) => { Log(e.Exception.ToString()); MessageBox.Show(e.Exception.Message, L.T("战士报告")); e.Handled = true; };
            app.Startup += (_, _) => app.Start(args);
            app.Run();
            return 0;
        }
        catch (Exception ex) { Log(ex.ToString()); MessageBox.Show(ex.Message, "For the Emperor"); return 1; }
    }
    private void Start(string[] args)
    {
        if ((!Preview && !L.Supported(Settings.Language)) || Array.Exists(args, a => a == "--choose-language"))
        {
            var language = new LanguageWindow(L.Language);
            if (language.ShowDialog() != true) { Shutdown(); return; }
            Settings.Language = language.SelectedLanguage;
            L.SetLanguage(language.SelectedLanguage);
            if (!Preview) Settings.Save();
        }
        Pet = new PetWindow(this); Pet.Show();
        if (!Preview)
        {
            _ = Task.Run(Native.RefreshDesktop);
            _ = Task.Run(() => Bridge.Listen(command => Dispatcher.InvokeAsync(() => Receive(command)).Task, stop.Token));
            if (Settings.MenuEnabled) SetMenu(true);
            tray = new Forms.NotifyIcon { Text = L.T("For the Emperor · 桌面战士"), Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!), ContextMenuStrip = BuildTrayMenu(), Visible = true };
            tray.DoubleClick += (_, _) => ShowPanel();
        }
        ShowPanel();
        if (Array.Exists(args, a => a == "--smoke-test"))
        {
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            int ticks = 0;
            t.Tick += (_, _) => { ticks++; if (ticks == 2) { Panel?.Hide(); Pet.Demo(); } if (ticks == 15) { t.Stop(); Directory.CreateDirectory("artifacts"); System.IO.File.WriteAllText("artifacts/smoke-test.txt", Pet.Engine.Busy ? "FAIL: mission did not finish" : "PASS: WPF startup, animated demo, return to idle and shutdown"); Shutdown(); } };
            t.Start();
        }
    }
    private Forms.ContextMenuStrip BuildTrayMenu()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(L.T("打开指挥面板"), null, (_, _) => ShowPanel());
        menu.Items.Add(L.T("演练处决（不删除文件）"), null, (_, _) => Pet.Demo());
        menu.Items.Add(L.T("召回战士"), null, (_, _) => Pet.Recall());
        menu.Items.Add(L.T("取消当前任务"), null, (_, _) => Pet.CancelMission());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(L.T("退出并移除右键菜单"), null, (_, _) => Shutdown());
        return menu;
    }
    internal void ChangeLanguage(string language)
    {
        if (!L.Supported(language)) return;
        Settings.Language = language;
        L.SetLanguage(language);
        if (!Preview) Settings.Save();
        Pet.RefreshLanguage();
        if (tray != null)
        {
            var old = tray.ContextMenuStrip;
            tray.ContextMenuStrip = BuildTrayMenu();
            tray.Text = L.T("For the Emperor · 桌面战士");
            old?.Dispose();
        }
        if (registered) SetMenu(true);
        Panel?.Refresh();
    }
    private string Receive(Command c)
    {
        if (c.Action == "show") { ShowPanel(); return "OK"; }
        if (c.Action != "execute" || c.Path == null || !double.IsFinite(c.X) || !double.IsFinite(c.Y)) return L.T("无效任务。");
        if (!registered) return L.T("文件右键处决尚未启用。");
        return Pet.Execute(c.Path, new(c.X, c.Y));
    }
    internal void SetMenu(bool enabled)
    {
        if (Preview) { menuStatus = "预览模式 · 未注册系统菜单"; return; }
        try
        {
            if (enabled)
            {
                ShellMenu.Register(owner); registered = true;
                if (!watching)
                {
                    var p = Process.GetCurrentProcess();
                    var watch = Process.Start(new ProcessStartInfo(Environment.ProcessPath!) {
                        UseShellExecute = false, CreateNoWindow = true,
                        ArgumentList = { "--watch", p.Id.ToString(), p.StartTime.ToUniversalTime().Ticks.ToString(), owner }
                    }) ?? throw new IOException(L.T("无法启动菜单看护进程。"));
                    watch.Dispose(); watching = true;
                }
                menuStatus = "已连接 · Windows 11 请点“显示更多选项”";
            }
            else { ShellMenu.Remove(owner); registered = false; menuStatus = "已关闭文件右键处决"; }
            Settings.MenuEnabled = enabled; Settings.Save();
        }
        catch (Exception ex)
        {
            try { ShellMenu.Remove(owner); } catch { }
            registered = false; Settings.MenuEnabled = false; menuStatus = "菜单连接失败：" + ex.Message; Log(MenuStatus);
        }
        Panel?.Refresh();
    }
    internal void ShowPanel()
    {
        if (Panel == null) Panel = new CommandWindow(this);
        Panel.Show(); Panel.Activate(); Panel.Refresh();
    }
    internal void SelectChapter(int index)
    {
        if (Pet.Engine.Busy) { Pet.Say("任务完成后才能更换战团。"); return; }
        Settings.Chapter = index; if (!Preview) Settings.Save();
        Pet.SetChapter(index); Panel?.Refresh();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        stop.Cancel();
        if (registered) { try { ShellMenu.Remove(owner); } catch (Exception ex) { Log(ex.Message); } }
        if (!Preview) Settings.Save();
        tray?.Dispose(); Pet?.Dispose();
        try { mutex?.ReleaseMutex(); } catch { }
        mutex?.Dispose(); base.OnExit(e);
    }
    internal static void Log(string message)
    {
        try { string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ForTheEmperor"); Directory.CreateDirectory(dir); string path = Path.Combine(dir, "activity.log"); if (System.IO.File.Exists(path) && new FileInfo(path).Length > 1024 * 1024) System.IO.File.Move(path, path + ".previous", true); System.IO.File.AppendAllText(path, DateTime.Now.ToString("s") + " " + message + Environment.NewLine); } catch { }
    }
}
