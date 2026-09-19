using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace ForTheEmperor;

internal sealed class PetWindow : Window, IDisposable
{
    private readonly App app;
    internal readonly Mission Engine = new();
    private readonly MarineView marine = new();
    private readonly DispatcherTimer timer;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly FilePolicy policy = FilePolicy.Desktop();
    private FileStamp? targetFile;
    private Point home, destination, routeStart, targetPoint;
    private Point dragStart, dragHome;
    private bool pressed, dragged, demo, deletionPending, disposed;
    private double lastTick, bubbleUntil, nextIdle = 13, executionStarted;
    private EffectWindow? effect;
    private HwndSource? source;
    private bool hotkey;
    internal event Action? StatusChanged;
    private string status = "等待命令";
    internal string Status => L.T(status);
    private string lastReport = "战士已就位。为了帝皇。";
    internal string LastReport { get => L.T(lastReport); private set => lastReport = value; }
    internal bool Sound => app.Settings.Sound;
    public PetWindow(App app)
    {
        this.app = app;
        Width = 370 * app.Settings.Scale; Height = 300 * app.Settings.Scale;
        marine.VerticalReserve = 40;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent;
        Topmost = true; ShowInTaskbar = false; ShowActivated = false;
        Title = L.T("For the Emperor · 桌面战士");
        Content = marine; marine.ChapterIndex = app.Settings.Chapter;
        marine.Cursor = Cursors.Hand;
        marine.ToolTip = L.T("拖动移动 · 单击互动 · 双击指挥面板 · 右键切换战团");
        SourceInitialized += (_, _) => {
            Native.ToolWindow(this);
            source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            source?.AddHook(Hook);
            hotkey = Native.RegisterHotKey(new WindowInteropHelper(this).Handle, 71, 0x4003, 0x1B);
        };
        Loaded += (_, _) => { Recall(); Say("Courage and honour."); };
        MouseLeftButtonDown += (_, e) => {
            if (e.ClickCount == 2) { app.ShowPanel(); return; }
            if (Engine.Busy) { Say("正在执行任务 · Ctrl+Alt+Esc 可取消"); return; }
            pressed = true; dragged = false; dragStart = Native.Cursor(); dragHome = Native.Foot(this); marine.CaptureMouse();
        };
        MouseMove += (_, _) => {
            if (!pressed) return;
            var delta = Native.Cursor() - dragStart;
            if (delta.Length > 5) dragged = true;
            if (dragged) Native.MoveFoot(this, Constrain(dragHome + delta));
        };
        MouseLeftButtonUp += (_, _) => {
            if (!pressed) return;
            pressed = false; marine.ReleaseMouseCapture();
            if (!dragged) { Say(Chapter.All[marine.ChapterIndex].Quote); if (Sound) System.Media.SystemSounds.Asterisk.Play(); }
        };
        LostMouseCapture += (_, _) => pressed = false;
        ContextMenu = BuildMenu();
        Engine.Changed += Changed;
        Engine.Impact += Impact;
        timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(25) };
        timer.Tick += Tick; timer.Start();
    }
    private ContextMenu BuildMenu()
    {
        var menu = new ContextMenu();
        void Item(string title, Action action) { var item = new MenuItem { Header = L.T(title) }; item.Click += (_, _) => action(); menu.Items.Add(item); }
        Item("打开指挥面板", app.ShowPanel);
        var chapters = new MenuItem { Header = L.T("更换战团") };
        for (int i = 0; i < Chapter.All.Length; i++) { int index = i; var item = new MenuItem { Header = L.T(Chapter.All[i].Name) }; item.Click += (_, _) => app.SelectChapter(index); chapters.Items.Add(item); }
        menu.Items.Add(chapters);
        Item("演练处决（不删除文件）", Demo);
        Item("取消任务  Ctrl+Alt+Esc", CancelMission);
        Item("召回右下角", Recall);
        menu.Items.Add(new Separator()); Item("退出并移除右键菜单", () => app.Shutdown());
        return menu;
    }
    internal void RefreshLanguage()
    {
        Title = L.T("For the Emperor · 桌面战士");
        marine.ToolTip = L.T("拖动移动 · 单击互动 · 双击指挥面板 · 右键切换战团");
        if (ContextMenu != null) ContextMenu.IsOpen = false;
        ContextMenu = BuildMenu();
        marine.InvalidateVisual();
        effect?.View.InvalidateVisual();
        StatusChanged?.Invoke();
    }
    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == 0x0312 && wParam.ToInt32() == 71) { CancelMission(); handled = true; }
        if (msg == 0x007E && !Engine.Busy) Dispatcher.BeginInvoke(Recall);
        return IntPtr.Zero;
    }
    internal void SetChapter(int i) { marine.ChapterIndex = i; Say(Chapter.All[i].Quote); marine.InvalidateVisual(); }
    internal void SetScale(double size)
    {
        if (Engine.Busy) return;
        var foot = Native.Foot(this); Width = 370 * size; Height = 300 * size; UpdateLayout(); Native.MoveFoot(this, Constrain(foot));
    }
    internal void Say(string text) { marine.Bubble = text; bubbleUntil = clock.Elapsed.TotalSeconds + 4; marine.InvalidateVisual(); }
    internal void Recall()
    {
        if (Engine.Busy) { Say("任务完成后可以召回。"); return; }
        var screen = Forms.Screen.FromPoint(new System.Drawing.Point((int)Native.Cursor().X, (int)Native.Cursor().Y)).WorkingArea;
        Native.MoveFoot(this, Constrain(new(screen.Right - 150 * Native.Dpi(this), screen.Bottom - 30 * Native.Dpi(this))));
    }
    private Point Constrain(Point foot)
    {
        var s = Forms.Screen.FromPoint(new System.Drawing.Point((int)foot.X, (int)foot.Y)).WorkingArea;
        double dpi = Native.Dpi(this), half = Width * dpi / 2, height = Height * dpi - 22 * dpi;
        return new(Math.Clamp(foot.X, s.Left + half, Math.Max(s.Left + half, s.Right - half)), Math.Clamp(foot.Y, s.Top + height, Math.Max(s.Top + height, s.Bottom - 22 * dpi)));
    }
    internal string Execute(string path, Point target)
    {
        if (app.Preview) return L.T("预览模式不执行文件删除。");
        if (Engine.Busy) return L.T("战士正在执行任务，请等待其返回后再试。");
        try { targetFile = policy.Capture(path); }
        catch (Exception ex) { return ex.Message; }
        Begin(target, false); return "OK";
    }
    internal void Demo()
    {
        if (Engine.Busy) { Say("战士正在执行任务。"); return; }
        targetFile = null;
        var foot = Native.Foot(this);
        var s = Forms.Screen.FromPoint(new System.Drawing.Point((int)foot.X, (int)foot.Y)).WorkingArea;
        Begin(new(Math.Max(s.Left + 110, foot.X - 380), Math.Max(s.Top + 170, foot.Y - 75)), true);
    }
    private void Begin(Point target, bool isDemo)
    {
        if (pressed) { pressed = false; marine.ReleaseMouseCapture(); }
        app.Panel?.Hide();
        demo = isDemo; home = Native.Foot(this); targetPoint = target;
        Engine.ChapterIndex = marine.ChapterIndex;
        var style = CombatStyle.All[marine.ChapterIndex];
        double side = target.X < home.X ? 1 : -1;
        destination = Constrain(new(target.X + side * style.StandOff * Native.Dpi(this) * app.Settings.Scale, target.Y + style.FootOffset * Native.Dpi(this) * app.Settings.Scale));
        routeStart = home;
        Engine.RunDuration = Math.Clamp((destination - home).Length / 540, .65, 4.0);
        effect = new EffectWindow(marine.ChapterIndex, demo); effect.Show(); effect.Position(target);
        if (!isDemo && targetFile != null) effect.View.FileIcon = Native.FileIcon(targetFile.Path);
        LastReport = isDemo ? "演练中 · 不涉及真实文件" : "目标：" + System.IO.Path.GetFileName(targetFile!.Path);
        Engine.Start();
    }
    internal void CancelMission()
    {
        if (Engine.Cancel()) { effect?.Close(); effect = null; LastReport = "任务已取消，文件未删除。"; Say("任务取消。返回阵地。"); StatusChanged?.Invoke(); }
        else if (Engine.Busy) Say("处决已命中；可在回收站恢复文件。");
    }
    private void Changed(Phase phase)
    {
        status = phase switch { Phase.Idle => "等待命令", Phase.Alert => "发现目标", Phase.Turn => "转向目标", Phase.Run => "前往目标", Phase.Arrive => "抵达目标", Phase.Attack => "发动攻击", Phase.Execution => "执行处决", Phase.Recover => "整理装备", Phase.Return => "返回阵地", _ => "" };
        if (phase == Phase.Alert) Say(demo ? "训练任务。武器就绪。" : "Target acquired.");
        if (phase == Phase.Turn) marine.Facing = destination.X >= home.X ? 1 : -1;
        if (phase == Phase.Arrive) marine.Facing = targetPoint.X >= destination.X ? 1 : -1;
        if (phase == Phase.Attack) Say(Chapter.All[marine.ChapterIndex].Weapon);
        if (phase == Phase.Execution) executionStarted = clock.Elapsed.TotalSeconds;
        if (phase == Phase.Return)
        {
            routeStart = Native.Foot(this); marine.Facing = home.X >= routeStart.X ? 1 : -1;
            Engine.RunDuration = Math.Clamp((home - routeStart).Length / 540, .65, 4.0);
        }
        if (phase == Phase.Idle) { Native.MoveFoot(this, Constrain(home)); effect?.Close(); effect = null; targetFile = null; marine.Facing = 1; nextIdle = clock.Elapsed.TotalSeconds + 16; }
        StatusChanged?.Invoke();
    }
    private async void Impact()
    {
        if (Sound) System.Media.SystemSounds.Exclamation.Play();
        if (demo) { if (effect != null) effect.View.TargetRemoved = true; LastReport = "演练完成 · 没有删除任何文件"; Say("Purge complete. [演练]"); return; }
        if (targetFile == null) { LastReport = "目标丢失；任务已取消。"; return; }
        deletionPending = true;
        var snapshot = targetFile;
        try
        {
            await OnSta(() => { policy.Validate(snapshot); Recycle.File(snapshot.Path, () => policy.Validate(snapshot)); });
            if (effect != null) effect.View.TargetRemoved = true;
            LastReport = "已移入回收站：" + System.IO.Path.GetFileName(snapshot.Path);
            Say("Purge complete."); App.Log(LastReport);
        }
        catch (Exception ex) { LastReport = "处决未完成：" + ex.Message; Say("处决未完成 · 请查看指挥面板"); App.Log(LastReport); }
        finally { deletionPending = false; StatusChanged?.Invoke(); }
    }
    private static Task OnSta(Action work)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => { try { work(); completion.SetResult(); } catch (Exception ex) { completion.SetException(ex); } }) { IsBackground = false, Name = "Recycle operation" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); return completion.Task;
    }
    private void Tick(object? sender, EventArgs args)
    {
        double now = clock.Elapsed.TotalSeconds, dt = now - lastTick; lastTick = now;
        if (!deletionPending) Engine.Tick(dt);
        if (Engine.Phase == Phase.Run) Native.MoveFoot(this, home + (destination - home) * Engine.Progress);
        if (Engine.Phase == Phase.Return) Native.MoveFoot(this, routeStart + (home - routeStart) * Engine.Progress);
        marine.Clock = now; marine.Phase = Engine.Phase; marine.Progress = Engine.Progress; marine.StateTime = Engine.Time;
        if (now > bubbleUntil) marine.Bubble = null;
        if (!Engine.Busy && now > nextIdle) { Say(Chapter.All[marine.ChapterIndex].Quote); nextIdle = now + 24 + marine.ChapterIndex * 2; }
        marine.InvalidateVisual();
        if (effect != null)
        {
            effect.View.Impact = Engine.Committed;
            effect.View.Phase = Engine.Phase;
            effect.View.Progress = Engine.Progress;
            effect.View.Clock = now;
            effect.View.Time = Engine.Phase >= Phase.Execution ? now - executionStarted : 0;
            Point foot = Native.Foot(this);
            double dpi = Native.Dpi(this), size = app.Settings.Scale;
            Point muzzle = new(foot.X + marine.Facing * 90 * dpi * size, foot.Y - CombatStyle.All[marine.ChapterIndex].MuzzleHeight * dpi * size);
            var relative = (muzzle - targetPoint) / Native.Dpi(effect);
            effect.View.Source = new(relative.X, relative.Y);
            effect.View.InvalidateVisual();
        }
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true; timer.Stop(); effect?.Close();
        if (hotkey) Native.UnregisterHotKey(new WindowInteropHelper(this).Handle, 71);
        source?.RemoveHook(Hook);
    }
}

internal sealed class EffectWindow : Window
{
    internal readonly EffectView View;
    public EffectWindow(int chapter, bool demo)
    {
        Width = 520; Height = 440; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent; Topmost = true; ShowInTaskbar = false; ShowActivated = false; IsHitTestVisible = false;
        View = new EffectView { ChapterIndex = chapter, Demo = demo }; Content = View;
        SourceInitialized += (_, _) => Native.ToolWindow(this, true);
    }
    internal void Position(Point p)
    {
        var h = new WindowInteropHelper(this).Handle; Native.GetWindowRect(h, out var r);
        Native.SetWindowPos(h, IntPtr.Zero, (int)(p.X - (r.Right - r.Left) / 2.0), (int)(p.Y - (r.Bottom - r.Top) / 2.0), 0, 0, 0x15);
    }
}
